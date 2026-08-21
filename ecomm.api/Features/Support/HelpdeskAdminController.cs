using ecomm.api.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.Support;

/// <summary>Merchant controls for the chatbot (v4 Phase 2) — on/off, active hours, and the
/// "what the bot couldn't answer" content-gap log, alongside the existing FAQ admin area since
/// unanswered questions are literally that content's own gap list.</summary>
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/helpdesk")]
public sealed class HelpdeskAdminController(IHelpdeskSettingsService settings) : ControllerBase
{
    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings(CancellationToken ct)
        => Ok(ApiResponse<HelpdeskSettingsDto>.Ok(await settings.GetAsync(ct)));

    [HttpPut("settings")]
    public async Task<IActionResult> UpdateSettings(UpdateHelpdeskSettingsRequest request, CancellationToken ct)
        => Ok(ApiResponse<HelpdeskSettingsDto>.Ok(await settings.UpdateAsync(request, ct), "Helpdesk settings updated."));

    [HttpGet("unanswered")]
    public async Task<IActionResult> Unanswered([FromQuery] int take = 100, CancellationToken ct = default)
        => Ok(ApiResponse<IReadOnlyList<UnansweredQuestionDto>>.Ok(await settings.UnansweredAsync(take, ct)));
}
