using System.Security.Claims;
using ecomm.api.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.Settings;

/// <summary>
/// Go-live housekeeping: clear the shop's test trading history, then close the door behind you.
///
/// settings.manage throughout — this is not order handling, and the Staff and Accountant roles
/// have no business anywhere near it.
/// </summary>
[ApiController]
[Route("api/admin/data-reset")]
[Authorize(Policy = ecomm.api.Common.Security.Perm.SettingsManage)]
public sealed class DataResetController : ControllerBase
{
    private readonly IDataResetService _reset;

    public DataResetController(IDataResetService reset) => _reset = reset;

    private long? CurrentUserId =>
        long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id)
            ? id
            : null;

    /// <summary>Every number the screen shows. Reads only — nothing is touched.</summary>
    [HttpGet]
    public async Task<IActionResult> Preview(CancellationToken ct)
        => Ok(ApiResponse<ResetPreview>.Ok(await _reset.PreviewAsync(ct)));

    [HttpPost]
    public async Task<IActionResult> Reset([FromBody] ResetRequest req, CancellationToken ct)
    {
        var result = await _reset.ResetAsync(
            req.Confirm,
            new ResetOptions(req.CustomerAccounts, req.Traffic, req.ImportHistory, req.Contacts),
            CurrentUserId,
            ct);

        return Ok(ApiResponse<ResetResult>.Ok(
            result,
            $"Cleared {result.Removed.Orders} order(s), {result.Removed.Invoices} invoice(s) and "
            + $"{result.Removed.Payments} payment(s). The catalogue is untouched."));
    }

    /// <summary>
    /// Marks the shop live, which permanently disables the reset above. One way only: there is
    /// deliberately no endpoint to undo it.
    /// </summary>
    [HttpPost("go-live")]
    public async Task<IActionResult> GoLive(CancellationToken ct)
    {
        var when = await _reset.GoLiveAsync(ct);
        return Ok(ApiResponse<object>.Ok(
            new { liveSince = when },
            "Marked live. Bulk deletion of trading records is now switched off for good."));
    }
}

public sealed record ResetRequest(
    string Confirm,
    bool CustomerAccounts = false,
    bool Traffic = false,
    bool ImportHistory = false,
    bool Contacts = false);
