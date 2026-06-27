namespace ecomm.api.Features.Auth.Services;

public sealed class JwtSettings
{
    public string Issuer { get; set; } = "ecomm.api";
    public string Audience { get; set; } = "ecomm.web";
    public string Key { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 60;
    public int RefreshTokenDays { get; set; } = 7;
}
