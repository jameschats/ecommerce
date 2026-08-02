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
    Task<AuthResponse> GuestCheckoutAsync(GuestCheckoutRequest request, string? ip, CancellationToken ct = default);
    Task RequestOtpAsync(OtpRequestDto request, CancellationToken ct = default);
    Task<AuthResponse> VerifyOtpAsync(OtpVerifyDto request, string? ip, CancellationToken ct = default);
    Task<AuthResponse> GoogleAsync(GoogleLoginRequest request, string? ip, CancellationToken ct = default);
    Task<AuthResponse> RefreshAsync(RefreshRequest request, string? ip, CancellationToken ct = default);
    Task RequestPasswordResetAsync(string email, CancellationToken ct = default);
    Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default);
    Task RequestEmailVerificationAsync(long userId, CancellationToken ct = default);
    Task<bool> ConfirmEmailVerificationAsync(long userId, string code, CancellationToken ct = default);
    /// <summary>Issue access + refresh tokens for an already-provisioned user (e.g. auto-login after onboarding).</summary>
    Task<AuthResponse> IssueTokensForUserAsync(User user, string? ip, CancellationToken ct = default);
}

public sealed class AuthService : IAuthService
{
    private long DefaultTenantId => _db.CurrentTenantId;
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

    /// <summary>
    /// Guest checkout (option 1 of the guest-checkout design fork, 2026-08-02): silently provisions a
    /// passwordless account for the email so the rest of checkout — quote/place/pay/order-history — is
    /// the exact same authenticated flow every logged-in shopper already uses, unchanged. Not gated
    /// behind the Email/Password provider toggle since, from the shopper's side, no password is ever
    /// typed or shown — this is a distinct mechanism, not password sign-in.
    ///
    /// An existing REAL (password-protected) account with the same email is a hard stop: silently
    /// attaching an order to somebody else's account just because you know their email would be an
    /// account-takeover-adjacent info leak, so this asks them to sign in instead. An existing
    /// passwordless account (a prior guest checkout, or an OTP/Google-only signup) is safe to reuse —
    /// nothing about the response reveals to the caller which case applies, matching the anti-
    /// enumeration posture the rest of this service already uses.
    /// </summary>
    public async Task<AuthResponse> GuestCheckoutAsync(GuestCheckoutRequest request, string? ip, CancellationToken ct = default)
    {
        var email = (request.Email ?? string.Empty).Trim();
        if (email.Length == 0 || !email.Contains('@'))
            throw new AppException("A valid email is required.");

        var normalized = email.ToUpperInvariant();
        var existing = await _db.Users.FirstOrDefaultAsync(
            u => u.TenantId == DefaultTenantId && u.NormalizedEmail == normalized && !u.IsDeleted, ct);

        if (existing is not null)
        {
            if (!string.IsNullOrEmpty(existing.PasswordHash))
                throw new AppException("An account already exists with this email. Please sign in to continue.", StatusCodes.Status409Conflict);
            if (!existing.IsActive)
                throw new AppException("Your account is disabled.", StatusCodes.Status403Forbidden);

            existing.LastLoginAt = DateTime.UtcNow;
            if (string.IsNullOrWhiteSpace(existing.FullName)) existing.FullName = request.FullName;
            if (string.IsNullOrWhiteSpace(existing.PhoneNumber) && await IsPhoneAvailableAsync(request.PhoneNumber, existing.UserId, ct))
                existing.PhoneNumber = request.PhoneNumber;
            await _db.SaveChangesAsync(ct);
            return await IssueTokensAsync(existing, ip, ct);
        }

        var now = DateTime.UtcNow;
        var user = new User
        {
            TenantId = DefaultTenantId,
            Email = email,
            NormalizedEmail = normalized,
            FullName = request.FullName,
            // A phone already tied to a different account is left off rather than failing checkout over
            // it — `Users.PhoneNumber` is unique per tenant (used for OTP login), but the real shipping
            // contact number lives on the CustomerAddress created right after this, which has no such
            // constraint. Two guests plausibly sharing a phone (family, a typo) shouldn't be a hard stop.
            PhoneNumber = await IsPhoneAvailableAsync(request.PhoneNumber, null, ct) ? request.PhoneNumber : null,
            IsActive = true,
            CreatedAt = now,
            // PasswordHash intentionally left null — this is what marks the account as guest-created.
            // They can turn it into a real password-login account anytime via the ordinary "forgot
            // password" flow, which now also accepts passwordless accounts (see RequestPasswordResetAsync).
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        await AssignRoleAsync(user, CustomerRole, ct);
        return await IssueTokensAsync(user, ip, ct);
    }

    /// <summary>True if nobody else in this tenant already holds this phone number — `Users.PhoneNumber`
    /// has a per-tenant unique constraint (`uq_users_tenant_phone`), so blindly assigning a colliding
    /// number throws a raw DbUpdateException instead of a clean, handled error.</summary>
    private async Task<bool> IsPhoneAvailableAsync(string? phone, long? excludeUserId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(phone)) return false;
        return !await _db.Users.AnyAsync(
            u => u.TenantId == DefaultTenantId && u.PhoneNumber == phone && u.UserId != (excludeUserId ?? 0), ct);
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
        // Anti-enumeration: silently no-op unless the account exists. Deliberately NOT excluding
        // passwordless accounts (guest checkout, OTP/Google-only signups) — for them this is really
        // "set your first password," not "reset," and it's the only way a guest-checkout account can
        // ever gain password-login capability. Same OTP-gated flow either way, no security change.
        if (user is null || string.IsNullOrWhiteSpace(user.Email)) return;
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

    public async Task<AuthResponse> RefreshAsync(RefreshRequest request, string? ip, CancellationToken ct = default)
    {
        var hash = _jwt.HashRefreshToken(request.RefreshToken ?? "");
        var token = await _db.RefreshTokens.Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (token is null || token.RevokedAt is not null || token.ExpiresAt < DateTime.UtcNow || token.User is null)
            throw new AppException("Invalid or expired refresh token.", StatusCodes.Status401Unauthorized);

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

    public Task<AuthResponse> IssueTokensForUserAsync(User user, string? ip, CancellationToken ct = default)
        => IssueTokensAsync(user, ip, ct);

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
