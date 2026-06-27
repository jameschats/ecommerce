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
    (string raw, string hash, DateTime expiresAtUtc) CreateRefreshToken();
    string HashRefreshToken(string raw);
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

    public (string raw, string hash, DateTime expiresAtUtc) CreateRefreshToken()
    {
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        return (raw, HashRefreshToken(raw), DateTime.UtcNow.AddDays(_settings.RefreshTokenDays));
    }

    public string HashRefreshToken(string raw) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
}
