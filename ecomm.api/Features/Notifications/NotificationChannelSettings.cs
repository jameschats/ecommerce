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
/// from SMS to email OTP when SMS is disabled. Absent Settings row = enabled (opt-out, not opt-in),
/// so this needs no seed/migration — every tenant starts with all three channels on.
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
    private long Tenant => db.CurrentTenantId;
    private static string KeyFor(string channelKey) => $"Channel{channelKey}Enabled";

    public async Task<bool> IsEnabledAsync(string channelKey, CancellationToken ct = default)
    {
        var v = await db.Settings.AsNoTracking()
            .Where(s => s.TenantId == Tenant && s.SettingKey == KeyFor(channelKey))
            .Select(s => s.SettingValue).FirstOrDefaultAsync(ct);
        return v is null || string.Equals(v, "true", StringComparison.OrdinalIgnoreCase);
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
