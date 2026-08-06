namespace ecomm.api.Features.Auth;

/// <summary>Canonical provider keys stored in AuthProviders.Provider.</summary>
public static class AuthProviderNames
{
    public const string EmailPassword = "EmailPassword";
    public const string MobileOtp = "MobileOtp";

    /// <summary>
    /// Email OTP. Listed so admin can order and show/hide it on the login page, but the
    /// endpoint behind it is deliberately never gated on this flag — see
    /// AuthService.RequestEmailOtpAsync. The quick-order checkout gate depends on it
    /// unconditionally, and it is the only way back in for accounts that have no password.
    /// </summary>
    public const string EmailOtp = "EmailOtp";
    public const string Google = "Google";
}

/// <summary>OTP purposes stored in OtpVerifications.Purpose.</summary>
public static class OtpPurpose
{
    public const string Login = "Login";
    public const string Register = "Register";
    public const string ResetPassword = "ResetPassword";
    public const string VerifyEmail = "VerifyEmail";
}
