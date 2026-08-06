using ecomm.api.Common.Exceptions;
using ecomm.api.Common.Models;
using ecomm.api.Data.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Notifications;

public sealed record TemplateDto(
    long NotificationTemplateId, string Code, string Channel, string? Subject, string? Body,
    bool IsActive, DateTime? UpdatedAt);

public sealed record SaveTemplateRequest(string? Subject, string? Body, bool IsActive);

public sealed record TemplatePreviewDto(string Subject, string Body);

/// <summary>
/// Message templates, editable at last.
///
/// The nine seeded templates were described in code as "admin-editable" and had no
/// controller, service or screen — the only way to change the wording a customer receives
/// was raw SQL against the database. Codes and channels are fixed here: they are wired to
/// send sites in C#, so inventing a new one from a form would create a template nothing
/// ever sends.
/// </summary>
[ApiController]
[Route("api/admin/notification-templates")]
[Authorize(Policy = ecomm.api.Common.Security.Perm.SettingsManage)]
public sealed class TemplatesAdminController : ControllerBase
{
    private const long Tenant = 1;

    /// <summary>
    /// Tokens the renderer substitutes, per template code. Shown beside the editor so the
    /// placeholders are discoverable rather than folklore — a typo'd token renders as
    /// literal braces in a customer's inbox.
    /// </summary>
    private static readonly Dictionary<string, string[]> TokensByCode = new(StringComparer.OrdinalIgnoreCase)
    {
        ["OrderConfirmation"] = ["CustomerName", "OrderNumber", "OrderTotal", "StoreName"],
        ["OrderStatusUpdate"] = ["CustomerName", "OrderNumber", "Status", "StoreName"],
        ["OrderShipped"] = ["CustomerName", "OrderNumber", "Courier", "TrackingNumber", "StoreName"],
        ["OrderCancelled"] = ["CustomerName", "OrderNumber", "StoreName"],
        ["EmailVerification"] = ["CustomerName", "OtpCode", "StoreName"],
        ["PasswordReset"] = ["CustomerName", "OtpCode", "StoreName"],
    };

    private readonly EcommerceDbContext _db;
    public TemplatesAdminController(EcommerceDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var items = await _db.NotificationTemplates
            .Where(t => t.TenantId == Tenant)
            .OrderBy(t => t.Code).ThenBy(t => t.Channel)
            .Select(t => new TemplateDto(
                t.NotificationTemplateId, t.Code, t.Channel, t.Subject, t.Body, t.IsActive, t.UpdatedAt))
            .ToListAsync(ct);

        return Ok(ApiResponse<List<TemplateDto>>.Ok(items));
    }

    /// <summary>The tokens available for a given code, keyed by code for the editor.</summary>
    [HttpGet("tokens")]
    public IActionResult Tokens() => Ok(ApiResponse<Dictionary<string, string[]>>.Ok(TokensByCode));

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Save(long id, [FromBody] SaveTemplateRequest req, CancellationToken ct)
    {
        var t = await _db.NotificationTemplates
            .FirstOrDefaultAsync(x => x.NotificationTemplateId == id && x.TenantId == Tenant, ct)
            ?? throw new AppException("Template not found.", StatusCodes.Status404NotFound);

        // An email with no body is worse than no email: the customer gets a blank message
        // and no indication anything went wrong.
        if (t.Channel == "Email" && req.IsActive && string.IsNullOrWhiteSpace(req.Body))
            throw new AppException("An active email template needs a body.");

        t.Subject = req.Subject?.Trim();
        t.Body = req.Body;
        t.IsActive = req.IsActive;
        t.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Ok(ApiResponse<TemplateDto>.Ok(
            new TemplateDto(t.NotificationTemplateId, t.Code, t.Channel, t.Subject, t.Body, t.IsActive, t.UpdatedAt),
            "Template saved."));
    }

    /// <summary>
    /// Renders the template with sample values, so the tokens can be checked before a real
    /// customer is the one who finds the mistake.
    /// </summary>
    [HttpPost("{id:long}/preview")]
    public async Task<IActionResult> Preview(long id, [FromBody] SaveTemplateRequest req, CancellationToken ct)
    {
        var t = await _db.NotificationTemplates
            .FirstOrDefaultAsync(x => x.NotificationTemplateId == id && x.TenantId == Tenant, ct)
            ?? throw new AppException("Template not found.", StatusCodes.Status404NotFound);

        var storeName = await _db.Settings
            .Where(s => s.SettingKey == "Site.Name").Select(s => s.SettingValue).FirstOrDefaultAsync(ct);

        var sample = new Dictionary<string, string>
        {
            ["CustomerName"] = "Ravi Kumar",
            ["OrderNumber"] = "DCS2608060001",
            ["OrderTotal"] = "₹1,225",
            ["Status"] = "Dispatched",
            ["Courier"] = "Professional Couriers",
            ["TrackingNumber"] = "PC123456789IN",
            ["OtpCode"] = "000000",
            ["StoreName"] = string.IsNullOrWhiteSpace(storeName) ? "CalendarShop" : storeName,
        };

        // Previews what is on screen, not what is saved — the point is to check an edit
        // before committing to it.
        return Ok(ApiResponse<TemplatePreviewDto>.Ok(new TemplatePreviewDto(
            Render(req.Subject ?? t.Subject ?? "", sample),
            Render(req.Body ?? t.Body ?? "", sample))));
    }

    /// <summary>Same {{token}} substitution the sender uses, so a preview cannot flatter.</summary>
    private static string Render(string template, Dictionary<string, string> values) =>
        System.Text.RegularExpressions.Regex.Replace(
            template, @"\{\{\s*(\w+)\s*\}\}",
            m => values.TryGetValue(m.Groups[1].Value, out var v) ? v : m.Value);
}
