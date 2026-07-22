using System.Security.Claims;
using ecomm.api.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ecomm.api.Features.Contact;

/// <summary>Public storefront contact form. Anonymous + IP rate-limited.</summary>
[ApiController]
[AllowAnonymous]
[Route("api/contact")]
public sealed class ContactController(IContactService contact) : ControllerBase
{
    [HttpPost]
    [EnableRateLimiting("contact")]
    public async Task<IActionResult> Submit(SubmitContactRequest request, CancellationToken ct)
    {
        await contact.SubmitAsync(request, ct);
        // Always the same response — a honeypot rejection must look identical to a success.
        return Ok(ApiResponse<object>.Ok(new { }, "Thanks — we'll get back to you shortly."));
    }
}

/// <summary>Merchant-admin inbox for storefront contact messages.</summary>
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/messages")]
public sealed class ContactAdminController(IContactService contact) : ControllerBase
{
    private long? CurrentUserId =>
        long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id) ? id : null;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(ApiResponse<PagedResult<ContactMessageDto>>.Ok(await contact.ListAsync(status, page, pageSize, ct)));

    [HttpGet("new-count")]
    public async Task<IActionResult> NewCount(CancellationToken ct)
        => Ok(ApiResponse<object>.Ok(new { count = await contact.NewCountAsync(ct) }));

    [HttpPut("{id:long}/status")]
    public async Task<IActionResult> SetStatus(long id, [FromQuery] string status, CancellationToken ct)
        => Ok(ApiResponse<ContactMessageDto>.Ok(await contact.SetStatusAsync(id, status, CurrentUserId, ct), "Message updated."));
}
