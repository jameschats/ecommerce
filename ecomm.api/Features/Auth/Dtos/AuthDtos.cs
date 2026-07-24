namespace ecomm.api.Features.Auth.Dtos;

// --- Requests ---
public sealed record RegisterRequest(string Email, string Password, string? FullName, string? PhoneNumber);
public sealed record LoginRequest(string Email, string Password);
public sealed record OtpRequestDto(string PhoneNumber);
public sealed record OtpVerifyDto(string PhoneNumber, string Code);
public sealed record GoogleLoginRequest(string IdToken);
public sealed record RefreshRequest(string RefreshToken);
public sealed record ForgotPasswordRequest(string Email);
public sealed record ResetPasswordRequest(string Email, string Code, string NewPassword);
public sealed record VerifyEmailRequest(string Code);

// --- Responses ---
public sealed record AuthUserDto(
    long UserId, string? Email, string? FullName, string? PhoneNumber, IReadOnlyList<string> Roles);

public sealed record AuthResponse(
    string AccessToken, string RefreshToken, DateTime ExpiresAtUtc, AuthUserDto User);

public sealed record AuthProviderDto(
    string Provider, string? DisplayName, bool IsEnabled, bool AllowRegistration, int DisplayOrder, string? ClientId);

public sealed record AuthConfigResponse(IReadOnlyList<AuthProviderDto> Providers);

// --- Admin ---
public sealed record UpdateAuthProviderRequest(bool IsEnabled, bool AllowRegistration, int DisplayOrder, string? ClientId);

public sealed record EmailOtpRequestDto(string Email);
public sealed record EmailOtpVerifyDto(string Email, string Code);
