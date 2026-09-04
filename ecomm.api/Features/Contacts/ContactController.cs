using System.Security.Claims;
using ecomm.api.Common.Exceptions;
using ecomm.api.Common.Models;
using ecomm.api.Data.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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

    /// <summary>
    /// A bulk-requirement enquiry — a lead, not a general question.
    ///
    /// Its own endpoint rather than a source the caller passes in: this is a public,
    /// unauthenticated route, and letting the request choose how it is filed would let anyone
    /// label their message whatever they liked. The requirement and design number are folded
    /// into the subject and message, so the inbox, CSV export and campaign audiences all read
    /// it without needing to know an enquiry from any other contact.
    /// </summary>
    [HttpPost("enquiry")]
    public async Task<IActionResult> Enquiry([FromBody] SubmitEnquiryRequest req, CancellationToken ct)
    {
        var userId = long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : (long?)null;

        var requirement = string.IsNullOrWhiteSpace(req.Requirement) ? "Bulk enquiry" : req.Requirement.Trim();
        var design = string.IsNullOrWhiteSpace(req.DesignNo) ? null : req.DesignNo.Trim();

        var message = string.IsNullOrWhiteSpace(req.Details) ? "" : req.Details.Trim();
        if (design is not null) message = $"Design no: {design}\n\n{message}";

        await _contacts.SubmitAsync(
            new SubmitContactRequest(
                req.Name, req.Email, req.Phone,
                Subject: requirement,
                Message: message,
                SourcePage: req.SourcePage ?? "/enquiry",
                Website: req.Website),
            userId, ct, source: "Enquiry");

        return Ok(ApiResponse<object>.Ok(
            new { received = true },
            "Thanks — we have your requirement and will send you a quote shortly."));
    }
}

/// <summary>The admin inbox: read enquiries, work through them, export the list.</summary>
[ApiController]
[Route("api/admin/contacts")]
[Authorize(Policy = ecomm.api.Common.Security.Perm.CustomerView)]
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

    // Reading the inbox is customer.view; changing it is customer.manage. An accountant can
    // look up who wrote in without being able to rewrite or delete the record.
    [HttpPut("{id:long}")]
    [Authorize(Policy = ecomm.api.Common.Security.Perm.CustomerManage)]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateContactRequest req, CancellationToken ct)
    {
        var updated = await _contacts.UpdateAsync(id, req, ct);
        return updated is null
            ? NotFound(ApiResponse<object>.Fail("Contact not found."))
            : Ok(ApiResponse<ContactDto>.Ok(updated, "Saved."));
    }

    [HttpPost]
    [Authorize(Policy = ecomm.api.Common.Security.Perm.CustomerManage)]
    public async Task<IActionResult> Create([FromBody] CreateContactRequest req, CancellationToken ct)
        => Ok(ApiResponse<ContactDto>.Ok(await _contacts.CreateAsync(req, ct), "Contact added."));

    [HttpDelete("{id:long}")]
    [Authorize(Policy = ecomm.api.Common.Security.Perm.CustomerManage)]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
        => await _contacts.DeleteAsync(id, ct)
            ? Ok(ApiResponse<object>.Ok(new { id }, "Contact deleted."))
            : NotFound(ApiResponse<object>.Fail("Contact not found."));

    [HttpPost("{id:long}/email")]
    [Authorize(Policy = ecomm.api.Common.Security.Perm.CustomerManage)]
    public async Task<IActionResult> SendEmail(long id, [FromBody] SendContactEmailRequest req, CancellationToken ct)
    {
        await _contacts.SendEmailAsync(id, req.Subject, req.Body, ct);
        return Ok(ApiResponse<object>.Ok(new { id }, "Email sent."));
    }

    /// <summary>
    /// Starting points for the reply box. The templates screen itself is settings.manage —
    /// administrator-only — but reading a body to paste into a reply is part of answering an
    /// enquiry, so this narrow read sits with the rest of contact management.
    /// </summary>
    [HttpGet("email-templates")]
    public async Task<IActionResult> EmailTemplates(
        [FromServices] EcommerceDbContext db, CancellationToken ct)
    {
        var items = await db.NotificationTemplates.AsNoTracking()
            .Where(t => t.Channel == "Email" && t.IsActive)
            .OrderBy(t => t.Code)
            .Select(t => new { t.Code, t.Subject, t.Body })
            .ToListAsync(ct);
        return Ok(ApiResponse<object>.Ok(items));
    }

    [HttpPost("import")]
    [Authorize(Policy = ecomm.api.Common.Security.Perm.CustomerManage)]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<IActionResult> Import(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) throw new AppException("Choose a file to import.");

        await using var stream = file.OpenReadStream();
        var result = await _contacts.ImportAsync(stream, file.FileName, ct);
        return Ok(ApiResponse<ContactImportResult>.Ok(
            result, $"{result.Added} added, {result.Updated} updated, {result.Skipped} skipped."));
    }

    /// <summary>
    /// The whole list as CSV, honouring the current filter. Served from the server rather
    /// than built in the browser so an export is not silently limited to the page on screen.
    /// </summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export(
        [FromQuery] string? status, [FromQuery] string? search, CancellationToken ct)
    {
        // Paged through rather than asked for in one go: ListAsync clamps PageSize to 100, so
        // the single call this used to make silently produced an export of at most 100 rows —
        // exactly the truncation the comment above claims it avoids.
        const int PageSize = 100;
        var items = new List<ContactDto>();
        for (var page = 1; ; page++)
        {
            var batch = await _contacts.ListAsync(new ContactQuery(status, search, page, PageSize), ct);
            items.AddRange(batch.Items);
            if (items.Count >= batch.TotalCount || batch.Items.Count == 0) break;
        }
        var all = new PagedResult<ContactDto> { Items = items, Page = 1, PageSize = items.Count, TotalCount = items.Count };

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
