using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Support;

/// <summary>
/// Merchant controls the chatbot: null start/end means "always active." Active hours change only
/// the post-escalation messaging (per the resolved solo-seller design decision — the bot doesn't
/// try harder or skip triggers outside hours, it still escalates on the same conditions; what's
/// different is only what the customer is told happens next).
/// </summary>
public sealed record HelpdeskSettingsDto(bool ChatbotEnabled, string? ActiveHoursStart, string? ActiveHoursEnd);
public sealed record UpdateHelpdeskSettingsRequest(bool ChatbotEnabled, string? ActiveHoursStart, string? ActiveHoursEnd);
public sealed record UnansweredQuestionDto(long Id, long? ConversationId, string Question, DateTime CreatedAt);

public interface IHelpdeskSettingsService
{
    Task<HelpdeskSettingsDto> GetAsync(CancellationToken ct = default);
    Task<HelpdeskSettingsDto> UpdateAsync(UpdateHelpdeskSettingsRequest req, CancellationToken ct = default);

    /// <summary>The content-gap feedback loop: what the bot couldn't answer, newest first — the
    /// merchant's signal for what FAQ content is actually worth adding.</summary>
    Task<IReadOnlyList<UnansweredQuestionDto>> UnansweredAsync(int take = 100, CancellationToken ct = default);
}

public sealed class HelpdeskSettingsService(EcommerceDbContext db) : IHelpdeskSettingsService
{
    private long Tenant => db.CurrentTenantId;
    private static readonly string[] Keys = ["ChatbotEnabled", "ChatbotActiveHoursStart", "ChatbotActiveHoursEnd"];

    public async Task<HelpdeskSettingsDto> GetAsync(CancellationToken ct = default)
    {
        var s = await db.Settings.Where(x => x.TenantId == Tenant && Keys.Contains(x.SettingKey))
            .ToDictionaryAsync(x => x.SettingKey, x => x.SettingValue, ct);
        // Defaults to enabled — a merchant who never touches this setting gets the bot working,
        // matching how every other AI feature in v4 ships opt-out rather than opt-in.
        var enabled = !string.Equals(s.GetValueOrDefault("ChatbotEnabled"), "false", StringComparison.OrdinalIgnoreCase);
        return new HelpdeskSettingsDto(enabled, s.GetValueOrDefault("ChatbotActiveHoursStart"), s.GetValueOrDefault("ChatbotActiveHoursEnd"));
    }

    public async Task<HelpdeskSettingsDto> UpdateAsync(UpdateHelpdeskSettingsRequest req, CancellationToken ct = default)
    {
        var (start, end) = ValidateHours(req.ActiveHoursStart, req.ActiveHoursEnd);
        await UpsertAsync("ChatbotEnabled", req.ChatbotEnabled ? "true" : "false", ct);
        await UpsertAsync("ChatbotActiveHoursStart", start, ct);
        await UpsertAsync("ChatbotActiveHoursEnd", end, ct);
        await db.SaveChangesAsync(ct);
        return await GetAsync(ct);
    }

    public async Task<IReadOnlyList<UnansweredQuestionDto>> UnansweredAsync(int take = 100, CancellationToken ct = default) =>
        await db.ChatbotUnansweredQuestions.AsNoTracking()
            .OrderByDescending(q => q.ChatbotUnansweredQuestionId)
            .Take(Math.Clamp(take, 1, 500))
            .Select(q => new UnansweredQuestionDto(q.ChatbotUnansweredQuestionId, q.SupportTicketId, q.Question, q.CreatedAt))
            .ToListAsync(ct);

    private static (string? start, string? end) ValidateHours(string? start, string? end)
    {
        if (string.IsNullOrWhiteSpace(start) && string.IsNullOrWhiteSpace(end)) return (null, null);
        if (!TimeOnly.TryParse(start, out _) || !TimeOnly.TryParse(end, out _))
            throw new AppException("Active hours must both be set as valid times (HH:mm), or both left empty.");
        return (start!.Trim(), end!.Trim());
    }

    private async Task UpsertAsync(string key, string? value, CancellationToken ct)
    {
        var existing = await db.Settings.FirstOrDefaultAsync(x => x.TenantId == Tenant && x.SettingKey == key, ct);
        if (existing is null)
            db.Settings.Add(new Setting { TenantId = Tenant, SettingKey = key, SettingValue = value, DataType = "string", Category = "Helpdesk", CreatedAt = DateTime.UtcNow });
        else
            existing.SettingValue = value;
    }
}
