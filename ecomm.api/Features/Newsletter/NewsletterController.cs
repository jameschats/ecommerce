using ecomm.api.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.Newsletter;

public sealed record NewsletterSubscribeRequest(string Email);

[ApiController]
[Route("api/newsletter")]
public sealed class NewsletterController(INewsletterService svc) : ControllerBase
{
    /// <summary>Public: storefront newsletter signup for the current store.</summary>
    [HttpPost("subscribe")]
    public async Task<IActionResult> Subscribe([FromBody] NewsletterSubscribeRequest req, CancellationToken ct)
    {
        await svc.SubscribeAsync(req?.Email ?? "", "storefront", ct);
        return Ok(ApiResponse<object>.Ok(new { }, "Thanks — you're subscribed!"));
    }

    /// <summary>Merchant: list captured newsletter emails (for export/marketing).</summary>
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> List([FromQuery] int take, CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<NewsletterSubscriberDto>>.Ok(await svc.ListAsync(take <= 0 ? 1000 : take, ct)));
}
