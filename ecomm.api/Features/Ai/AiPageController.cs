using ecomm.api.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.Ai;

/// <summary>Merchant: generate a storefront page from a prompt (AI-5). Creates a draft page → edit in the builder.</summary>
[ApiController]
[Route("api/admin/ai/page")]
[Authorize(Roles = "Admin")]
public sealed class AiPageController(IAiPageService pages) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Generate([FromBody] GeneratePageRequest req, CancellationToken ct)
        => Ok(ApiResponse<GeneratedPageDto>.Ok(await pages.GenerateAsync(req, ct), "Page created — edit it in the builder."));
}
