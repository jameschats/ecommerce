using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Notifications;

public sealed record ChannelTogglesDto(bool EmailEnabled, bool SmsEnabled, bool WhatsAppEnabled);

/// <summary>
/// Per-tenant admin kill switch for each notification channel — lets a merchant force-disable
/// WhatsApp or SMS (e.g. while testing, or while a channel is mid-setup like the current WhatsApp
/// template approval) without touching provider config. A disabled channel is treated exactly like
/// one the recipient can't be reached on: <see cref="NotificationRouter"/> skips it and falls
/// through to the next channel in the chain, and <see cref="Auth.Services.OtpService"/> falls back
/// from SMS to email OTP when SMS is disabled.
/// <para>Absent Settings row falls back to <see cref="Defaults"/>, not a blanket "enabled" — as of
/// 2026-09-10, SMS and WhatsApp default OFF (SMS is blocked on DLT registration; WhatsApp has no
/// Meta-approved template yet) so a tenant with no rows at all runs on Email alone, the one channel
/// that's actually deliverable today. Needs no seed/migration: every existing and new tenant picks
/// up the right default the moment this ships.</para>
/// </summary>
public interface INotificationChannelSettings
{
    /// <param name="channelKey">Must match an <see cref="INotificationChannel.Key"/> exactly ("Email"|"SMS"|"WhatsApp").</param>
    Task<bool> IsEnabledAsync(string channelKey, CancellationToken ct = default);
    Task<ChannelTogglesDto> GetAsync(CancellationToken ct = default);
    Task<ChannelTogglesDto> UpdateAsync(ChannelTogglesDto req, CancellationToken ct = default);
}

public sealed class NotificationChannelSettings(EcommerceDbContext db) : INotificationChannelSettings
{
    /// <summary>Fallback when no Settings row exists yet for a channel. Only Email starts on.</summary>
    private static readonly Dictionary<string, bool> Defaults = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Email"] = true,
        ["SMS"] = false,
        ["WhatsApp"] = false,
    };

    private long Tenant => db.CurrentTenantId;
    private static string KeyFor(string channelKey) => $"Channel{channelKey}Enabled";

    public async Task<bool> IsEnabledAsync(string channelKey, CancellationToken ct = default)
    {
        var v = await db.Settings.AsNoTracking()
            .Where(s => s.TenantId == Tenant && s.SettingKey == KeyFor(channelKey))
            .Select(s => s.SettingValue).FirstOrDefaultAsync(ct);
        if (v is not null) return string.Equals(v, "true", StringComparison.OrdinalIgnoreCase);
        return Defaults.GetValueOrDefault(channelKey, true);
    }

    public async Task<ChannelTogglesDto> GetAsync(CancellationToken ct = default) => new(
        await IsEnabledAsync("Email", ct), await IsEnabledAsync("SMS", ct), await IsEnabledAsync("WhatsApp", ct));

    public async Task<ChannelTogglesDto> UpdateAsync(ChannelTogglesDto req, CancellationToken ct = default)
    {
        await UpsertAsync("Email", req.EmailEnabled, ct);
        await UpsertAsync("SMS", req.SmsEnabled, ct);
        await UpsertAsync("WhatsApp", req.WhatsAppEnabled, ct);
        await db.SaveChangesAsync(ct);
        return req;
    }

    private async Task UpsertAsync(string channelKey, bool enabled, CancellationToken ct)
    {
        var key = KeyFor(channelKey);
        var value = enabled ? "true" : "false";
        var existing = await db.Settings.FirstOrDefaultAsync(x => x.TenantId == Tenant && x.SettingKey == key, ct);
        if (existing is null)
            db.Settings.Add(new Setting { TenantId = Tenant, SettingKey = key, SettingValue = value, DataType = "string", Category = "Notifications", CreatedAt = DateTime.UtcNow });
        else
            existing.SettingValue = value;
    }
}
