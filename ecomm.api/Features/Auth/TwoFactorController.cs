using System.Security.Claims;
using ecomm.api.Common.Models;
using ecomm.api.Features.Auth.Dtos;
using ecomm.api.Features.Auth.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.Auth;

/// <summary>2FA enrollment/management for the currently-logged-in user — always self-service (you
/// manage your own 2FA, an admin can't enable/disable it for someone else). Bare [Authorize], not
/// role-scoped: every account type (Customer included) can technically reach this, but only
/// Merchant Admin/Super Admin/Staff logins actually check TwoFactorEnabled at sign-in (AuthService.
/// LoginAsync) — a customer enabling it would just be inert today, harmless to leave open rather
/// than special-case it out.</summary>
[ApiController]
[Route("api/auth/2fa")]
[Authorize]
public sealed class TwoFactorController(ITwoFactorService twoFactor) : ControllerBase
{
    private long UserId =>
        long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id) ? id : 0;

    [HttpGet("status")]
    public async Task<IActionResult> Status(CancellationToken ct)
        => Ok(ApiResponse<TwoFactorStatusDto>.Ok(new TwoFactorStatusDto(await twoFactor.IsEnabledAsync(UserId, ct))));

    [HttpPost("enroll")]
    public async Task<IActionResult> BeginEnrollment(CancellationToken ct)
        => Ok(ApiResponse<TwoFactorEnrollmentDto>.Ok(await twoFactor.BeginEnrollmentAsync(UserId, ct)));

    /// <summary>Confirms enrollment with a real generated code; returns 10 backup codes shown exactly
    /// once — the frontend must make the user acknowledge saving them before continuing.</summary>
    [HttpPost("enroll/confirm")]
    public async Task<IActionResult> ConfirmEnrollment(TwoFactorEnrollConfirmRequest request, CancellationToken ct)
        => Ok(ApiResponse<TwoFactorBackupCodesDto>.Ok(
            new TwoFactorBackupCodesDto(await twoFactor.ConfirmEnrollmentAsync(UserId, request.Code, ct)),
            "Two-factor authentication is now enabled."));

    [HttpPost("disable")]
    public async Task<IActionResult> Disable(TwoFactorCodeRequest request, CancellationToken ct)
    {
        await twoFactor.DisableAsync(UserId, request.Code, ct);
        return Ok(ApiResponse<object>.Ok(new { disabled = true }, "Two-factor authentication has been turned off."));
    }

    [HttpPost("backup-codes/regenerate")]
    public async Task<IActionResult> RegenerateBackupCodes(TwoFactorCodeRequest request, CancellationToken ct)
        => Ok(ApiResponse<TwoFactorBackupCodesDto>.Ok(
            new TwoFactorBackupCodesDto(await twoFactor.RegenerateBackupCodesAsync(UserId, request.Code, ct))));
}
