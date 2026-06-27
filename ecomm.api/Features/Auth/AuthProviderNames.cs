namespace ecomm.api.Features.Auth;

/// <summary>Canonical provider keys stored in AuthProviders.Provider.</summary>
public static class AuthProviderNames
{
    public const string EmailPassword = "EmailPassword";
    public const string MobileOtp = "MobileOtp";
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
