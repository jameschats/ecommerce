using ecomm.api.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace ecomm.api.Features.Faqs;

/// <summary>Public FAQ list for the storefront.</summary>
[ApiController]
[AllowAnonymous]
[Route("api/faq")]
public sealed class FaqController(IFaqService faqs) : ControllerBase
{
    [HttpGet]
    [OutputCache(PolicyName = "public")]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<FaqDto>>.Ok(await faqs.PublishedAsync(ct)));
}

/// <summary>Merchant-admin FAQ management.</summary>
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/faq")]
public sealed class FaqAdminController(IFaqService faqs) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<FaqDto>>.Ok(await faqs.AllAsync(ct)));

    [HttpPost]
    public async Task<IActionResult> Create(SaveFaqRequest request, CancellationToken ct)
        => Ok(ApiResponse<FaqDto>.Ok(await faqs.CreateAsync(request, ct), "FAQ added."));

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, SaveFaqRequest request, CancellationToken ct)
        => Ok(ApiResponse<FaqDto>.Ok(await faqs.UpdateAsync(id, request, ct), "FAQ updated."));

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await faqs.DeleteAsync(id, ct);
        return Ok(ApiResponse<object>.Ok(new { }, "FAQ removed."));
    }
}
