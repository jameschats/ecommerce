using ecomm.api.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Notifications;

/// <summary>One switchable notification, as the admin screen presents it.</summary>
public sealed record NotificationSwitchDto(
    string Event, string Label, string Description, string Recipient, bool Email, bool Sms, bool HasSms);

/// <summary>
/// Whether a given notification should go out at all.
///
/// Consulted at every automatic send point. Sign-in and account-recovery messages are
/// deliberately absent: an OTP or a password reset that can be switched off is a way to lock
/// every customer out of their own account, and the symptom would look like a broken site
/// rather than a setting somebody changed. Those always send.
///
/// Defaults to on. A notification nobody has expressed an opinion about should behave the way
/// it did before this existed.
/// </summary>
public interface INotificationPolicy
{
    Task<bool> IsEnabledAsync(string @event, string channel, CancellationToken ct = default);
    Task<List<NotificationSwitchDto>> ListAsync(CancellationToken ct = default);
    Task SetAsync(string @event, string channel, bool enabled, CancellationToken ct = default);
}

public sealed class NotificationPolicy : INotificationPolicy
{
    private const long Tenant = 1;

    /// <summary>
    /// The switchable events. Order is the order the screen shows them in — the lifecycle as
    /// it happens, so the list reads like the journey a customer goes through.
    /// </summary>
    private static readonly (string Event, string Label, string Description, string Recipient, bool HasSms)[] Known =
    {
        ("OrderPlaced", "Order received", "Sent the moment an order is placed, before payment.", "Customer", false),
        ("OrderPlacedAdmin", "New order alert", "Tells you an order has come in.", "You", false),
        ("PaymentConfirmed", "Payment received", "Confirms you have the money.", "Customer", false),
        ("OrderConfirmation", "Order confirmed", "Sent when a paid order is confirmed.", "Customer", true),
        ("OrderShipped", "Order dispatched", "Carries the courier name and tracking number.", "Customer", true),
        ("OrderDelivered", "Order delivered", "Sent when you mark the order delivered.", "Customer", true),
        ("OrderStatusUpdate", "Status changed", "Any other status change made from admin.", "Customer", false),
        ("OrderCancelled", "Order cancelled", "Sent when an order is cancelled.", "Customer", true),
        ("AbandonedEstimate", "Abandoned estimate reminder", "Nudges someone who built a basket and left.", "Customer", false),
    };

    private readonly EcommerceDbContext _db;
    public NotificationPolicy(EcommerceDbContext db) => _db = db;

    private static string Key(string @event, string channel) => $"Notify.{@event}.{channel}";

    public async Task<bool> IsEnabledAsync(string @event, string channel, CancellationToken ct = default)
    {
        // Unknown events are not switchable and always send — that is what keeps OTP and
        // password reset out of reach of this screen rather than merely off it.
        if (!Known.Any(k => k.Event == @event)) return true;

        var value = await _db.Settings.AsNoTracking()
            .Where(s => s.TenantId == Tenant && s.SettingKey == Key(@event, channel))
            .Select(s => s.SettingValue)
            .FirstOrDefaultAsync(ct);

        // Absent means on. Only an explicit "false" switches something off.
        return !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<List<NotificationSwitchDto>> ListAsync(CancellationToken ct = default)
    {
        var settings = await _db.Settings.AsNoTracking()
            .Where(s => s.TenantId == Tenant && s.SettingKey.StartsWith("Notify."))
            .ToDictionaryAsync(s => s.SettingKey, s => s.SettingValue, ct);

        bool On(string @event, string channel) =>
            !settings.TryGetValue(Key(@event, channel), out var v)
            || !string.Equals(v, "false", StringComparison.OrdinalIgnoreCase);

        return Known.Select(k => new NotificationSwitchDto(
            k.Event, k.Label, k.Description, k.Recipient,
            On(k.Event, "Email"),
            k.HasSms && On(k.Event, "SMS"),
            k.HasSms)).ToList();
    }

    public async Task SetAsync(string @event, string channel, bool enabled, CancellationToken ct = default)
    {
        if (!Known.Any(k => k.Event == @event))
            throw new Common.Exceptions.AppException("That notification cannot be switched off.");
        if (channel is not ("Email" or "SMS"))
            throw new Common.Exceptions.AppException("Channel must be Email or SMS.");

        var key = Key(@event, channel);
        var row = await _db.Settings.FirstOrDefaultAsync(s => s.TenantId == Tenant && s.SettingKey == key, ct);

        if (row is null)
        {
            _db.Settings.Add(new Data.Entities.Setting
            {
                TenantId = Tenant, SettingKey = key,
                SettingValue = enabled ? "true" : "false", CreatedAt = DateTime.UtcNow,
            });
        }
        else
        {
            row.SettingValue = enabled ? "true" : "false";
            row.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
    }
}
