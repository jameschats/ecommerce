using System.Security.Claims;
using ecomm.api.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.PublicApi;

/// <summary>Merchant self-service management of their own webhook subscriptions.</summary>
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/webhooks")]
public sealed class WebhookAdminController(IWebhookSubscriptionService subscriptions) : ControllerBase
{
    private long? UserId =>
        long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id) ? id : null;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<WebhookSubscriptionDto>>.Ok(await subscriptions.ListAsync(ct)));

    [HttpGet("events")]
    public IActionResult Events() => Ok(ApiResponse<IReadOnlyList<string>>.Ok(WebhookSubscriptionService.ValidEvents));

    [HttpPost]
    public async Task<IActionResult> Create(CreateWebhookSubscriptionRequest request, CancellationToken ct)
        => Ok(ApiResponse<CreatedWebhookSubscriptionDto>.Ok(await subscriptions.CreateAsync(request, UserId, ct),
            "Copy this signing secret now — you won't be able to see it again."));

    [HttpPut("{id:long}/active")]
    public async Task<IActionResult> SetActive(long id, [FromBody] bool isActive, CancellationToken ct)
    {
        await subscriptions.SetActiveAsync(id, isActive, ct);
        return Ok(ApiResponse<object>.Ok(new { }, isActive ? "Subscription resumed." : "Subscription paused."));
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await subscriptions.DeleteAsync(id, ct);
        return Ok(ApiResponse<object>.Ok(new { }, "Subscription removed."));
    }
}
