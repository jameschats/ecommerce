using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using Ganss.Xss;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Notifications;

public sealed record NotificationTemplateDto(
    long Id, string Code, string Label, string Channel, string? Subject, string? Body,
    string? ExternalTemplateId, bool IsActive, DateTime? UpdatedAt);
public sealed record UpdateNotificationTemplateRequest(string? Subject, string? Body, string? ExternalTemplateId, bool IsActive);

public sealed record NotificationSenderDto(string? SenderName, string? ReplyToEmail);

public interface INotificationAdminService
{
    Task<IReadOnlyList<NotificationTemplateDto>> ListTemplatesAsync(CancellationToken ct = default);
    Task<NotificationTemplateDto> UpdateTemplateAsync(long id, UpdateNotificationTemplateRequest req, CancellationToken ct = default);
    Task<NotificationSenderDto> GetSenderAsync(CancellationToken ct = default);
    Task<NotificationSenderDto> UpdateSenderAsync(NotificationSenderDto req, CancellationToken ct = default);
}

/// <summary>
/// Merchant management of transactional notifications: edit the per-tenant message templates
/// (Email/SMS, <c>{{token}}</c> placeholders) and the sender identity (display name + reply-to).
/// Email HTML bodies are sanitized on write; SMS bodies are stored as plain text.
/// </summary>
public sealed class NotificationAdminService(EcommerceDbContext db) : INotificationAdminService
{
    private long Tenant => db.CurrentTenantId;

    /// <summary>Friendly labels for the known template codes (fallback: prettified code).</summary>
    private static readonly Dictionary<string, string> Labels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["OrderPlaced"] = "Order placed",
        ["OrderPaid"] = "Payment received",
        ["OrderShipped"] = "Order shipped",
        ["OrderDelivered"] = "Order delivered",
        ["OrderCancelled"] = "Order cancelled",
        ["OrderRefunded"] = "Order refunded",
        ["PasswordReset"] = "Password reset",
        ["EmailVerification"] = "Email verification",
        ["Welcome"] = "Welcome",
    };

    public async Task<IReadOnlyList<NotificationTemplateDto>> ListTemplatesAsync(CancellationToken ct = default)
    {
        var rows = await db.NotificationTemplates.AsNoTracking()
            .OrderBy(t => t.Code).ThenBy(t => t.Channel)
            .Select(t => new NotificationTemplateDto(
                t.NotificationTemplateId, t.Code, "", t.Channel, t.Subject, t.Body, t.ExternalTemplateId, t.IsActive, t.UpdatedAt))
            .ToListAsync(ct);
        return rows.Select(r => r with { Label = Labels.GetValueOrDefault(r.Code, Prettify(r.Code)) }).ToList();
    }

    public async Task<NotificationTemplateDto> UpdateTemplateAsync(long id, UpdateNotificationTemplateRequest req, CancellationToken ct = default)
    {
        var t = await db.NotificationTemplates.FirstOrDefaultAsync(x => x.NotificationTemplateId == id, ct)
            ?? throw new AppException("Template not found.", 404);

        // Email bodies are HTML → sanitize; SMS/WhatsApp bodies are plain text.
        t.Subject = req.Subject?.Trim();
        t.Body = t.Channel == "Email" ? new HtmlSanitizer().Sanitize(req.Body ?? "") : req.Body?.Trim();
        // Only WhatsApp rows use this (Meta's BSP-assigned template id) — harmless to store on any
        // channel, but the admin UI only ever shows/sends it for Channel="WhatsApp".
        t.ExternalTemplateId = req.ExternalTemplateId?.Trim();
        t.IsActive = req.IsActive;
        t.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return new NotificationTemplateDto(t.NotificationTemplateId, t.Code, Labels.GetValueOrDefault(t.Code, Prettify(t.Code)),
            t.Channel, t.Subject, t.Body, t.ExternalTemplateId, t.IsActive, t.UpdatedAt);
    }

    public async Task<NotificationSenderDto> GetSenderAsync(CancellationToken ct = default)
    {
        var rows = await db.Settings.AsNoTracking()
            .Where(s => s.TenantId == Tenant && (s.SettingKey == "SenderName" || s.SettingKey == "ReplyToEmail"))
            .ToDictionaryAsync(s => s.SettingKey, s => s.SettingValue, ct);
        return new NotificationSenderDto(rows.GetValueOrDefault("SenderName"), rows.GetValueOrDefault("ReplyToEmail"));
    }

    public async Task<NotificationSenderDto> UpdateSenderAsync(NotificationSenderDto req, CancellationToken ct = default)
    {
        var replyTo = req.ReplyToEmail?.Trim();
        if (!string.IsNullOrEmpty(replyTo) && !replyTo.Contains('@'))
            throw new AppException("Reply-to must be a valid email address.");

        await UpsertAsync("SenderName", req.SenderName?.Trim(), ct);
        await UpsertAsync("ReplyToEmail", replyTo, ct);
        await db.SaveChangesAsync(ct);
        return await GetSenderAsync(ct);
    }

    private async Task UpsertAsync(string key, string? value, CancellationToken ct)
    {
        var existing = await db.Settings.FirstOrDefaultAsync(x => x.TenantId == Tenant && x.SettingKey == key, ct);
        if (existing is null)
            db.Settings.Add(new Setting { TenantId = Tenant, SettingKey = key, SettingValue = value, DataType = "string", Category = "Notifications", CreatedAt = DateTime.UtcNow });
        else
            existing.SettingValue = value;
    }

    private static string Prettify(string code) =>
        System.Text.RegularExpressions.Regex.Replace(code, "(?<=[a-z])(?=[A-Z])", " ");
}
