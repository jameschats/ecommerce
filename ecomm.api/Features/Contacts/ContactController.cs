using System.Security.Claims;
using ecomm.api.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.Contacts;

/// <summary>
/// The public contact form. Anonymous by design — most people writing in do not have an
/// account, and requiring one would lose exactly the enquiries this exists to capture.
/// </summary>
[ApiController]
[Route("api/contact")]
[AllowAnonymous]
public sealed class ContactController : ControllerBase
{
    private readonly IContactService _contacts;
    public ContactController(IContactService contacts) => _contacts = contacts;

    [HttpPost]
    public async Task<IActionResult> Submit([FromBody] SubmitContactRequest req, CancellationToken ct)
    {
        // Attach the sender when they happen to be signed in, so their enquiries and their
        // orders can be seen together. Absence is normal, not an error.
        var userId = long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : (long?)null;

        await _contacts.SubmitAsync(req, userId, ct);
        return Ok(ApiResponse<object>.Ok(
            new { received = true },
            "Thanks — we have your message and will get back to you shortly."));
    }
}

/// <summary>The admin inbox: read enquiries, work through them, export the list.</summary>
[ApiController]
[Route("api/admin/contacts")]
[Authorize(Roles = "Admin")]
public sealed class ContactsAdminController : ControllerBase
{
    private readonly IContactService _contacts;
    public ContactsAdminController(IContactService contacts) => _contacts = contacts;

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? status, [FromQuery] string? search,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
        => Ok(ApiResponse<PagedResult<ContactDto>>.Ok(
            await _contacts.ListAsync(new ContactQuery(status, search, page, pageSize), ct)));

    /// <summary>Unworked enquiries, for the badge on the nav.</summary>
    [HttpGet("new-count")]
    public async Task<IActionResult> NewCount(CancellationToken ct)
        => Ok(ApiResponse<object>.Ok(new { count = await _contacts.NewCountAsync(ct) }));

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateContactRequest req, CancellationToken ct)
    {
        var updated = await _contacts.UpdateAsync(id, req, ct);
        return updated is null
            ? NotFound(ApiResponse<object>.Fail("Contact not found."))
            : Ok(ApiResponse<ContactDto>.Ok(updated, "Saved."));
    }

    /// <summary>
    /// The whole list as CSV, honouring the current filter. Served from the server rather
    /// than built in the browser so an export is not silently limited to the page on screen.
    /// </summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export(
        [FromQuery] string? status, [FromQuery] string? search, CancellationToken ct)
    {
        var all = await _contacts.ListAsync(new ContactQuery(status, search, 1, 100), ct);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Received,Name,Email,Phone,Subject,Message,Status,Subscribed,Source");
        foreach (var c in all.Items)
        {
            sb.AppendLine(string.Join(',', new[]
            {
                c.CreatedAt.ToString("yyyy-MM-dd HH:mm"), c.Name, c.Email ?? "", c.Phone ?? "",
                c.Subject ?? "", c.Message ?? "", c.Status, c.SubscribedToEmails ? "yes" : "no", c.Source,
            }.Select(Csv)));
        }

        return File(System.Text.Encoding.UTF8.GetBytes(sb.ToString()), "text/csv",
            $"contacts-{DateTime.UtcNow:yyyy-MM-dd}.csv");
    }

    /// <summary>Quotes every field: names contain commas and messages contain newlines.</summary>
    private static string Csv(string v) => $"\"{v.Replace("\"", "\"\"")}\"";
}
