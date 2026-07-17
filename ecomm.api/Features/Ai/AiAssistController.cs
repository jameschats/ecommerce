using ecomm.api.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.Ai;

/// <summary>Merchant "✨ Improve with AI" endpoints (AI-1). Metered through the credit ledger.</summary>
[ApiController]
[Route("api/admin/ai")]
[Authorize(Roles = "Admin")]
public sealed class AiAssistController(IAiImproveService improve, IAiService ai) : ControllerBase
{
    /// <summary>Cheap check the shared ✨ button uses to hide itself when AI is off (no DB, no spend).</summary>
    [HttpGet("status")]
    public IActionResult Status() => Ok(ApiResponse<AiStatusDto>.Ok(new AiStatusDto(ai.Enabled)));

    [HttpPost("improve")]
    public async Task<IActionResult> Improve([FromBody] ImproveRequest req, CancellationToken ct)
        => Ok(ApiResponse<AiTextDto>.Ok(new AiTextDto(await improve.ImproveAsync(req.Purpose, req.Text ?? string.Empty, req.Context, ct))));

    [HttpPost("seo")]
    public async Task<IActionResult> Seo([FromBody] SeoRequest req, CancellationToken ct)
        => Ok(ApiResponse<SeoResult>.Ok(await improve.SeoAsync(req.Name ?? string.Empty, req.Description, ct)));
}

public sealed record AiStatusDto(bool Enabled);
public sealed record AiTextDto(string Text);
public sealed record ImproveRequest(string Purpose, string? Text, string? Context);
public sealed record SeoRequest(string? Name, string? Description);
