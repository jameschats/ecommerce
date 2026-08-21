using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ecomm.api.Data.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ecomm.api.Features.Auth.Services;

public interface IJwtTokenService
{
    (string token, DateTime expiresAtUtc) CreateAccessToken(User user, IEnumerable<string> roles, IEnumerable<string> permissions);
    /// <summary>A short-lived token for a super-admin to act as a merchant. Carries imp/imp_mode/imp_by claims.</summary>
    (string token, DateTime expiresAtUtc) CreateImpersonationToken(User user, IEnumerable<string> roles, string mode, long impersonatorUserId, int minutes = 30);
    (string raw, string hash, DateTime expiresAtUtc) CreateRefreshToken();
    string HashRefreshToken(string raw);

    /// <summary>A short-lived token proving "this user's password (or OTP/Google) already checked out,
    /// pending their second factor" — issued instead of a real access token when 2FA is enabled.
    /// Deliberately NEVER wired into the ASP.NET Core JWT Bearer scheme (see Program.cs) - the
    /// frontend sends it back only as a plain field in the 2FA-verify request body, never as an
    /// Authorization header, so it can't accidentally authorize anything even if a caller tried.</summary>
    (string token, DateTime expiresAtUtc) CreateTwoFactorChallengeToken(long userId, int minutes = 5);
    /// <summary>Returns the user id if the token is a valid, unexpired, correctly-purposed 2FA
    /// challenge token — null for anything else (wrong purpose, expired, tampered, wrong key).</summary>
    long? ValidateTwoFactorChallengeToken(string token);
}

public sealed class JwtTokenService : IJwtTokenService
{
    public const string PermissionClaim = "perm";

    private readonly JwtSettings _settings;

    public JwtTokenService(IOptions<JwtSettings> settings) => _settings = settings.Value;

    public (string token, DateTime expiresAtUtc) CreateAccessToken(
        User user, IEnumerable<string> roles, IEnumerable<string> permissions)
    {
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(_settings.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("tenant", user.TenantId.ToString()),
        };
        if (!string.IsNullOrEmpty(user.Email)) claims.Add(new(JwtRegisteredClaimNames.Email, user.Email));
        if (!string.IsNullOrEmpty(user.FullName)) claims.Add(new("name", user.FullName));
        if (!string.IsNullOrEmpty(user.PhoneNumber)) claims.Add(new("phone", user.PhoneNumber));
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        claims.AddRange(permissions.Select(p => new Claim(PermissionClaim, p)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken(_settings.Issuer, _settings.Audience, claims, now, expires, creds);
        return (new JwtSecurityTokenHandler().WriteToken(jwt), expires);
    }

    public (string token, DateTime expiresAtUtc) CreateImpersonationToken(
        User user, IEnumerable<string> roles, string mode, long impersonatorUserId, int minutes = 30)
    {
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(minutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("tenant", user.TenantId.ToString()),
            new("imp", "1"),
            new("imp_mode", mode),            // view | full
            new("imp_by", impersonatorUserId.ToString()),
        };
        if (!string.IsNullOrEmpty(user.Email)) claims.Add(new(JwtRegisteredClaimNames.Email, user.Email));
        if (!string.IsNullOrEmpty(user.FullName)) claims.Add(new("name", user.FullName));
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken(_settings.Issuer, _settings.Audience, claims, now, expires, creds);
        return (new JwtSecurityTokenHandler().WriteToken(jwt), expires);
    }

    public (string raw, string hash, DateTime expiresAtUtc) CreateRefreshToken()
    {
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        return (raw, HashRefreshToken(raw), DateTime.UtcNow.AddDays(_settings.RefreshTokenDays));
    }

    public string HashRefreshToken(string raw) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));

    private const string TwoFactorPurposeClaim = "purpose";
    private const string TwoFactorPurposeValue = "2fa-challenge";

    public (string token, DateTime expiresAtUtc) CreateTwoFactorChallengeToken(long userId, int minutes = 5)
    {
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(minutes);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(TwoFactorPurposeClaim, TwoFactorPurposeValue),
        };
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken(_settings.Issuer, _settings.Audience, claims, now, expires, creds);
        return (new JwtSecurityTokenHandler().WriteToken(jwt), expires);
    }

    public long? ValidateTwoFactorChallengeToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Key));
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true, ValidIssuer = _settings.Issuer,
            ValidateAudience = true, ValidAudience = _settings.Audience,
            ValidateLifetime = true, ValidateIssuerSigningKey = true, IssuerSigningKey = key,
            ClockSkew = TimeSpan.FromSeconds(30),
        };
        try
        {
            var principal = new JwtSecurityTokenHandler().ValidateToken(token, parameters, out _);
            if (principal.FindFirstValue(TwoFactorPurposeClaim) != TwoFactorPurposeValue) return null;
            var sub = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
            return long.TryParse(sub, out var userId) ? userId : null;
        }
        catch { return null; }   // expired, tampered, wrong key — all just "invalid", not an error to surface
    }
}
