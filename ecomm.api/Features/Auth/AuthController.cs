using ecomm.api.Common.Models;
using ecomm.api.Features.Auth.Dtos;
using ecomm.api.Features.Auth.Services;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.Auth;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _auth;

    public AuthController(IAuthService auth) => _auth = auth;

    private string? Ip => HttpContext.Connection.RemoteIpAddress?.ToString();

    /// <summary>Enabled auth providers — the storefront renders the login page from this.</summary>
    [HttpGet("config")]
    public async Task<IActionResult> Config(CancellationToken ct)
        => Ok(ApiResponse<AuthConfigResponse>.Ok(await _auth.GetConfigAsync(ct)));

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken ct)
        => Ok(ApiResponse<AuthResponse>.Ok(await _auth.RegisterAsync(request, Ip, ct)));

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
        => Ok(ApiResponse<AuthResponse>.Ok(await _auth.LoginAsync(request, Ip, ct)));

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
}
