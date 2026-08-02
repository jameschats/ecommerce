using System.Security.Claims;
using ecomm.api.Common.Models;
using ecomm.api.Features.Auth.Dtos;
using ecomm.api.Features.Auth.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ecomm.api.Features.Auth;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _auth;

    public AuthController(IAuthService auth) => _auth = auth;

    private string? Ip => HttpContext.Connection.RemoteIpAddress?.ToString();
    private long CurrentUserId =>
        long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id) ? id : 0;

    /// <summary>Enabled auth providers — the storefront renders the login page from this.</summary>
    [HttpGet("config")]
    public async Task<IActionResult> Config(CancellationToken ct)
        => Ok(ApiResponse<AuthConfigResponse>.Ok(await _auth.GetConfigAsync(ct)));

    [EnableRateLimiting("auth")]
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken ct)
        => Ok(ApiResponse<AuthResponse>.Ok(await _auth.RegisterAsync(request, Ip, ct)));

    [EnableRateLimiting("auth")]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
        => Ok(ApiResponse<AuthResponse>.Ok(await _auth.LoginAsync(request, Ip, ct)));

    /// <summary>Guest checkout: silently provisions (or reuses) a passwordless account so an anonymous
    /// shopper can complete checkout without ever seeing a login/register screen.</summary>
    [EnableRateLimiting("auth")]
    [HttpPost("guest-checkout")]
    public async Task<IActionResult> GuestCheckout(GuestCheckoutRequest request, CancellationToken ct)
        => Ok(ApiResponse<AuthResponse>.Ok(await _auth.GuestCheckoutAsync(request, Ip, ct)));

    [EnableRateLimiting("auth")]
    [HttpPost("otp/request")]
    public async Task<IActionResult> OtpRequest(OtpRequestDto request, CancellationToken ct)
    {
        await _auth.RequestOtpAsync(request, ct);
        return Ok(ApiResponse<object>.Ok(new { sent = true }, "Verification code sent."));
    }

    [HttpPost("otp/verify")]
    public async Task<IActionResult> OtpVerify(OtpVerifyDto request, CancellationToken ct)
        => Ok(ApiResponse<AuthResponse>.Ok(await _auth.VerifyOtpAsync(request, Ip, ct)));

    [HttpPost("google")]
    public async Task<IActionResult> Google(GoogleLoginRequest request, CancellationToken ct)
        => Ok(ApiResponse<AuthResponse>.Ok(await _auth.GoogleAsync(request, Ip, ct)));

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(RefreshRequest request, CancellationToken ct)
        => Ok(ApiResponse<AuthResponse>.Ok(await _auth.RefreshAsync(request, Ip, ct)));

    /// <summary>Send a password-reset code by email. Always returns success (no account enumeration).</summary>
    [EnableRateLimiting("auth")]
    [HttpPost("password/forgot")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken ct)
    {
        await _auth.RequestPasswordResetAsync(request.Email, ct);
        return Ok(ApiResponse<object>.Ok(new { sent = true }, "If an account exists for that email, a reset code has been sent."));
    }

    [HttpPost("password/reset")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken ct)
    {
        await _auth.ResetPasswordAsync(request, ct);
        return Ok(ApiResponse<object>.Ok(new { reset = true }, "Your password has been updated. Please sign in."));
    }

    [Authorize]
    [HttpPost("email/verify/request")]
    public async Task<IActionResult> RequestEmailVerification(CancellationToken ct)
    {
        await _auth.RequestEmailVerificationAsync(CurrentUserId, ct);
        return Ok(ApiResponse<object>.Ok(new { sent = true }, "Verification code sent to your email."));
    }

    [Authorize]
    [HttpPost("email/verify/confirm")]
    public async Task<IActionResult> ConfirmEmailVerification(VerifyEmailRequest request, CancellationToken ct)
    {
        await _auth.ConfirmEmailVerificationAsync(CurrentUserId, request.Code, ct);
        return Ok(ApiResponse<object>.Ok(new { verified = true }, "Your email is verified."));
    }
}
