using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Auth.Dtos;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Auth.Services;

public interface IAuthService
{
    Task<AuthConfigResponse> GetConfigAsync(CancellationToken ct = default);
    Task<AuthResponse> RegisterAsync(RegisterRequest request, string? ip, CancellationToken ct = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, string? ip, CancellationToken ct = default);
    Task RequestOtpAsync(OtpRequestDto request, CancellationToken ct = default);
    Task<AuthResponse> VerifyOtpAsync(OtpVerifyDto request, string? ip, CancellationToken ct = default);
    Task RequestEmailOtpAsync(string email, CancellationToken ct = default);
    Task<AuthResponse> VerifyEmailOtpAsync(string email, string code, string? ip, CancellationToken ct = default);
    Task<AuthResponse> GoogleAsync(GoogleLoginRequest request, string? ip, CancellationToken ct = default);
    Task<AuthResponse> RefreshAsync(RefreshRequest request, string? ip, CancellationToken ct = default);
    Task RequestPasswordResetAsync(string email, CancellationToken ct = default);
    Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default);
    Task ChangePasswordAsync(long userId, ChangePasswordRequest request, CancellationToken ct = default);
    Task RequestEmailVerificationAsync(long userId, CancellationToken ct = default);
    Task<bool> ConfirmEmailVerificationAsync(long userId, string code, CancellationToken ct = default);
}

public sealed class AuthService : IAuthService
{
    private const long DefaultTenantId = 1;
    private const string CustomerRole = "CUSTOMER";
    private const int MaxFailedLogins = 5;
    private static readonly TimeSpan LockoutWindow = TimeSpan.FromMinutes(15);

    private readonly EcommerceDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly IJwtTokenService _jwt;
    private readonly IOtpService _otp;
    private readonly IGoogleTokenValidator _google;
    private readonly IAuthProviderService _providers;

    public AuthService(
        EcommerceDbContext db,
        IPasswordHasher hasher,
        IJwtTokenService jwt,
        IOtpService otp,
        IGoogleTokenValidator google,
        IAuthProviderService providers)
    {
        _db = db;
        _hasher = hasher;
        _jwt = jwt;
        _otp = otp;
        _google = google;
        _providers = providers;
    }

    public async Task<AuthConfigResponse> GetConfigAsync(CancellationToken ct = default)
    {
        var providers = await _providers.GetAllAsync(DefaultTenantId, ct);
        var dtos = providers
            .Where(p => p.IsEnabled)
            .Select(p => new AuthProviderDto(
                p.Provider, p.DisplayName, p.IsEnabled, p.AllowRegistration, p.DisplayOrder,
                p.Provider == AuthProviderNames.Google ? _providers.GetConfigValue(p, "clientId") : null))
            .ToList();
        return new AuthConfigResponse(dtos);
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, string? ip, CancellationToken ct = default)
    {
        var provider = await RequireEnabledProviderAsync(AuthProviderNames.EmailPassword, ct);
        if (!provider.AllowRegistration)
            throw new AppException("Self-registration is currently disabled.", StatusCodes.Status403Forbidden);

        var email = (request.Email ?? string.Empty).Trim();
        if (email.Length == 0 || !email.Contains('@'))
            throw new AppException("A valid email is required.");
        if ((request.Password ?? string.Empty).Length < 6)
            throw new AppException("Password must be at least 6 characters.");

        var normalized = email.ToUpperInvariant();
        if (await _db.Users.AnyAsync(u => u.TenantId == DefaultTenantId && u.NormalizedEmail == normalized, ct))
            throw new AppException("An account with this email already exists.", StatusCodes.Status409Conflict);

        var now = DateTime.UtcNow;
        var user = new User
        {
            TenantId = DefaultTenantId,
            Email = email,
            NormalizedEmail = normalized,
            PasswordHash = _hasher.Hash(request.Password!),
            FullName = request.FullName,
            PhoneNumber = request.PhoneNumber,
            IsActive = true,
            CreatedAt = now,
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        await AssignRoleAsync(user, CustomerRole, ct);
        return await IssueTokensAsync(user, ip, ct);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, string? ip, CancellationToken ct = default)
    {
        await RequireEnabledProviderAsync(AuthProviderNames.EmailPassword, ct);

        var normalized = (request.Email ?? string.Empty).Trim().ToUpperInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(
            u => u.TenantId == DefaultTenantId && u.NormalizedEmail == normalized && !u.IsDeleted, ct);

        var now = DateTime.UtcNow;
        if (user is not null && user.LockoutEndUtc is { } until && until > now)
            throw new AppException("Too many failed attempts. Try again in a few minutes.", StatusCodes.Status429TooManyRequests);

        if (user is null || string.IsNullOrEmpty(user.PasswordHash) || !_hasher.Verify(request.Password ?? "", user.PasswordHash))
        {
            if (user is not null) await RegisterFailedLoginAsync(user, now, ct);
            throw new AppException("Invalid email or password.", StatusCodes.Status401Unauthorized);
        }
        if (!user.IsActive)
            throw new AppException("Your account is disabled.", StatusCodes.Status403Forbidden);

        user.FailedLoginCount = 0;
        user.LockoutEndUtc = null;
        user.LastLoginAt = now;
        await _db.SaveChangesAsync(ct);
        return await IssueTokensAsync(user, ip, ct);
    }

    /// <summary>Count a failed password attempt; lock the account for a window once the limit is hit.</summary>
    private async Task RegisterFailedLoginAsync(User user, DateTime now, CancellationToken ct)
    {
        user.FailedLoginCount++;
        if (user.FailedLoginCount >= MaxFailedLogins)
        {
            user.LockoutEndUtc = now.Add(LockoutWindow);
            user.FailedLoginCount = 0;
        }
        user.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);
    }

    // ---------------- Password reset (email OTP) ----------------
    public async Task RequestPasswordResetAsync(string email, CancellationToken ct = default)
    {
        var normalized = (email ?? string.Empty).Trim().ToUpperInvariant();
        if (normalized.Length == 0) return;
        var user = await _db.Users.FirstOrDefaultAsync(
            u => u.TenantId == DefaultTenantId && u.NormalizedEmail == normalized && !u.IsDeleted && u.IsActive, ct);
        // Anti-enumeration: silently no-op unless the account exists and can use password login.
        if (user is null || string.IsNullOrEmpty(user.PasswordHash) || string.IsNullOrWhiteSpace(user.Email)) return;
        try { await _otp.RequestAsync(user.Email!.Trim().ToLowerInvariant(), "Email", "ResetPassword", ct); }
        catch (AppException) { /* swallow cooldown/rate-limit so the response is always uniform */ }
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default)
    {
        if ((request.NewPassword ?? string.Empty).Length < 6)
            throw new AppException("Password must be at least 6 characters.");
        var identifier = (request.Email ?? string.Empty).Trim().ToLowerInvariant();
        var ok = await _otp.VerifyAsync(identifier, "ResetPassword", request.Code ?? string.Empty, ct);
        if (!ok) throw new AppException("Invalid or expired code.");

        var normalized = identifier.ToUpperInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(
            u => u.TenantId == DefaultTenantId && u.NormalizedEmail == normalized && !u.IsDeleted, ct)
            ?? throw new AppException("Account not found.", StatusCodes.Status404NotFound);
        user.PasswordHash = _hasher.Hash(request.NewPassword!);
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    // ---------------- Email verification (email OTP) ----------------
    public async Task RequestEmailVerificationAsync(long userId, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId && u.TenantId == DefaultTenantId, ct)
            ?? throw new AppException("Account not found.", StatusCodes.Status404NotFound);
        if (string.IsNullOrWhiteSpace(user.Email)) throw new AppException("There's no email on file to verify.");
        if (user.IsEmailVerified) throw new AppException("Your email is already verified.");
        await _otp.RequestAsync(user.Email!.Trim().ToLowerInvariant(), "Email", "VerifyEmail", ct);
    }

    public async Task<bool> ConfirmEmailVerificationAsync(long userId, string code, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId && u.TenantId == DefaultTenantId, ct)
            ?? throw new AppException("Account not found.", StatusCodes.Status404NotFound);
        if (user.IsEmailVerified) return true;
        var identifier = (user.Email ?? string.Empty).Trim().ToLowerInvariant();
        var ok = await _otp.VerifyAsync(identifier, "VerifyEmail", code ?? string.Empty, ct);
        if (!ok) throw new AppException("Invalid or expired code.");
        user.IsEmailVerified = true;
        user.EmailVerifiedAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task RequestOtpAsync(OtpRequestDto request, CancellationToken ct = default)
    {
        await RequireEnabledProviderAsync(AuthProviderNames.MobileOtp, ct);
        var phone = NormalizePhone(request.PhoneNumber);
        await _otp.RequestAsync(phone, "SMS", OtpPurpose.Login, ct);
    }

    /// <summary>
    /// Email OTP login. Deliberately not gated on the Mobile-OTP provider flag: email OTP
    /// is free on Brevo where SMS costs per message, so this is the channel the quick-order
    /// login gate defaults to (design.md §9.6).
    /// </summary>
    public async Task RequestEmailOtpAsync(string email, CancellationToken ct = default)
    {
        var normalized = (email ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalized) || !normalized.Contains('@'))
            throw new AppException("Enter a valid email address.");

        await _otp.RequestAsync(normalized, "Email", OtpPurpose.Login, ct);
    }

    public async Task<AuthResponse> VerifyEmailOtpAsync(string email, string code, string? ip, CancellationToken ct = default)
    {
        var normalized = (email ?? string.Empty).Trim().ToUpperInvariant();

        var ok = await _otp.VerifyAsync(normalized, OtpPurpose.Login, code ?? "", ct);
        if (!ok) throw new AppException("Invalid verification code.", StatusCodes.Status401Unauthorized);

        var now = DateTime.UtcNow;
        var user = await _db.Users.FirstOrDefaultAsync(
            u => u.TenantId == DefaultTenantId && u.NormalizedEmail == normalized && !u.IsDeleted, ct);

        // First order creates the account — by the time an order exists, so does a login,
        // which is what makes "check my orders" work without a registration step.
        if (user is null)
        {
            user = new User
            {
                TenantId = DefaultTenantId,
                Email = email.Trim(),
                NormalizedEmail = normalized,
                EmailVerifiedAt = now,
                IsActive = true,
                CreatedAt = now,
            };
            _db.Users.Add(user);
            await _db.SaveChangesAsync(ct);
            await AssignRoleAsync(user, CustomerRole, ct);
        }
        else if (user.EmailVerifiedAt is null)
        {
            user.EmailVerifiedAt = now;
        }

        user.LastLoginAt = now;
        await _db.SaveChangesAsync(ct);
        return await IssueTokensAsync(user, ip, ct);
    }

    public async Task<AuthResponse> VerifyOtpAsync(OtpVerifyDto request, string? ip, CancellationToken ct = default)
    {
        await RequireEnabledProviderAsync(AuthProviderNames.MobileOtp, ct);
        var phone = NormalizePhone(request.PhoneNumber);

        var ok = await _otp.VerifyAsync(phone, OtpPurpose.Login, request.Code ?? "", ct);
        if (!ok) throw new AppException("Invalid verification code.", StatusCodes.Status401Unauthorized);

        var now = DateTime.UtcNow;
        var user = await _db.Users.FirstOrDefaultAsync(u => u.TenantId == DefaultTenantId && u.PhoneNumber == phone, ct);
        if (user is null)
        {
            user = new User
            {
                TenantId = DefaultTenantId,
                PhoneNumber = phone,
                IsPhoneVerified = true,
                PhoneVerifiedAt = now,
                IsActive = true,
                CreatedAt = now,
            };
            _db.Users.Add(user);
            await _db.SaveChangesAsync(ct);

            await AssignRoleAsync(user, CustomerRole, ct);
            _db.UserExternalLogins.Add(new UserExternalLogin
            {
                UserId = user.UserId,
                Provider = AuthProviderNames.MobileOtp,
                ProviderUserId = phone,
                CreatedAt = now,
            });
        }
        else if (!user.IsPhoneVerified)
        {
            user.IsPhoneVerified = true;
            user.PhoneVerifiedAt = now;
        }

        user.LastLoginAt = now;
        await _db.SaveChangesAsync(ct);
        return await IssueTokensAsync(user, ip, ct);
    }

    public async Task<AuthResponse> GoogleAsync(GoogleLoginRequest request, string? ip, CancellationToken ct = default)
    {
        var provider = await RequireEnabledProviderAsync(AuthProviderNames.Google, ct);
        var clientId = _providers.GetConfigValue(provider, "clientId");
        if (string.IsNullOrEmpty(clientId))
            throw new AppException("Google sign-in is not configured.");

        var info = await _google.ValidateAsync(request.IdToken ?? "", clientId, ct);
        if (info is null)
            throw new AppException("Invalid Google token.", StatusCodes.Status401Unauthorized);

        var now = DateTime.UtcNow;
        var link = await _db.UserExternalLogins
            .Include(l => l.User)
            .FirstOrDefaultAsync(l => l.Provider == AuthProviderNames.Google && l.ProviderUserId == info.Subject, ct);

        User user;
        if (link?.User is not null)
        {
            user = link.User;
        }
        else
        {
            // Auto-link by verified email, else create a new account.
            User? existing = null;
            if (!string.IsNullOrEmpty(info.Email) && info.EmailVerified)
            {
                var ne = info.Email.ToUpperInvariant();
                existing = await _db.Users.FirstOrDefaultAsync(u => u.TenantId == DefaultTenantId && u.NormalizedEmail == ne, ct);
            }

            if (existing is not null)
            {
                user = existing;
            }
            else
            {
                user = new User
                {
                    TenantId = DefaultTenantId,
                    Email = info.Email,
                    NormalizedEmail = info.Email?.ToUpperInvariant(),
                    FullName = info.Name,
                    IsEmailVerified = info.EmailVerified,
                    EmailVerifiedAt = info.EmailVerified ? now : null,
                    IsActive = true,
                    CreatedAt = now,
                };
                _db.Users.Add(user);
                await _db.SaveChangesAsync(ct);
                await AssignRoleAsync(user, CustomerRole, ct);
            }

            _db.UserExternalLogins.Add(new UserExternalLogin
            {
                UserId = user.UserId,
                Provider = AuthProviderNames.Google,
                ProviderUserId = info.Subject,
                Email = info.Email,
                CreatedAt = now,
            });
        }

        user.LastLoginAt = now;
        await _db.SaveChangesAsync(ct);
        return await IssueTokensAsync(user, ip, ct);
    }

    /// <summary>
    /// Change your own password while signed in.
    ///
    /// Staff accounts are created with a password an admin chose and passed on by hand, so
    /// there has to be a way to replace it that does not route back through that admin. The
    /// reset-by-email flow already existed but depends on mail actually arriving; this does not.
    /// </summary>
    public async Task ChangePasswordAsync(long userId, ChangePasswordRequest request, CancellationToken ct = default)
    {
        var newPassword = request.NewPassword ?? "";
        if (newPassword.Length < 6)
            throw new AppException("Password must be at least 6 characters.");

        var user = await _db.Users.FirstOrDefaultAsync(
            u => u.UserId == userId && u.TenantId == DefaultTenantId && !u.IsDeleted && u.IsActive, ct)
            ?? throw new AppException("Account not found.", StatusCodes.Status401Unauthorized);

        // An account created by OTP or Google has no password to confirm against; it is
        // setting one for the first time rather than changing it.
        if (!string.IsNullOrEmpty(user.PasswordHash)
            && !_hasher.Verify(request.CurrentPassword ?? "", user.PasswordHash))
            throw new AppException("Your current password is not correct.");

        user.PasswordHash = _hasher.Hash(newPassword);
        user.UpdatedAt = DateTime.UtcNow;

        // Every other session dies with the old password — that is most of the point of
        // changing it.
        foreach (var t in await _db.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAt == null).ToListAsync(ct))
            t.RevokedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
    }

    public async Task<AuthResponse> RefreshAsync(RefreshRequest request, string? ip, CancellationToken ct = default)
    {
        var hash = _jwt.HashRefreshToken(request.RefreshToken ?? "");
        var token = await _db.RefreshTokens.Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (token is null || token.RevokedAt is not null || token.ExpiresAt < DateTime.UtcNow || token.User is null)
            throw new AppException("Invalid or expired refresh token.", StatusCodes.Status401Unauthorized);

        // The person behind the token, not just the token. Refresh is a rolling grant — each
        // one mints a fresh 7-day token — so without this check an account that was switched
        // off or deleted keeps renewing itself forever and never returns to the login page,
        // where IsActive *is* checked. Deactivating someone has to end their access, or the
        // button that does it is decoration.
        if (!token.User.IsActive || token.User.IsDeleted)
        {
            token.RevokedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            throw new AppException("This account is no longer active.", StatusCodes.Status401Unauthorized);
        }

        var response = await IssueTokensAsync(token.User, ip, ct);

        token.RevokedAt = DateTime.UtcNow;
        token.ReplacedByHash = _jwt.HashRefreshToken(response.RefreshToken);
        await _db.SaveChangesAsync(ct);

        return response;
    }

    // --- Helpers ---

    private async Task<AuthProvider> RequireEnabledProviderAsync(string name, CancellationToken ct)
    {
        var provider = await _providers.GetAsync(DefaultTenantId, name, ct);
        if (provider is null || !provider.IsEnabled)
            throw new AppException($"{name} sign-in is currently disabled.", StatusCodes.Status403Forbidden);
        return provider;
    }

    private async Task AssignRoleAsync(User user, string normalizedRole, CancellationToken ct)
    {
        var role = await _db.Roles.FirstOrDefaultAsync(
            r => r.TenantId == user.TenantId && r.NormalizedName == normalizedRole, ct);
        if (role is null) return;

        var exists = await _db.UserRoles.AnyAsync(ur => ur.UserId == user.UserId && ur.RoleId == role.RoleId, ct);
        if (!exists)
        {
            _db.UserRoles.Add(new UserRole { UserId = user.UserId, RoleId = role.RoleId });
            await _db.SaveChangesAsync(ct);
        }
    }

    private async Task<AuthResponse> IssueTokensAsync(User user, string? ip, CancellationToken ct)
    {
        var roles = await _db.UserRoles.Where(ur => ur.UserId == user.UserId)
            .Join(_db.Roles, ur => ur.RoleId, r => r.RoleId, (ur, r) => r.Name)
            .ToListAsync(ct);

        var permissions = await (
            from ur in _db.UserRoles
            where ur.UserId == user.UserId
            join rp in _db.RolePermissions on ur.RoleId equals rp.RoleId
            join p in _db.Permissions on rp.PermissionId equals p.PermissionId
            select p.Code).Distinct().ToListAsync(ct);

        var (accessToken, accessExpires) = _jwt.CreateAccessToken(user, roles, permissions);
        var (rawRefresh, refreshHash, refreshExpires) = _jwt.CreateRefreshToken();

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.UserId,
            TokenHash = refreshHash,
            ExpiresAt = refreshExpires,
            CreatedByIp = ip,
            CreatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync(ct);

        var userDto = new AuthUserDto(user.UserId, user.Email, user.FullName, user.PhoneNumber, roles);
        return new AuthResponse(accessToken, rawRefresh, accessExpires, userDto);
    }

    private static string NormalizePhone(string? phone)
    {
        var trimmed = (phone ?? string.Empty).Trim().Replace(" ", "");
        if (trimmed.Length < 8)
            throw new AppException("A valid phone number is required.");
        return trimmed;
    }
}
