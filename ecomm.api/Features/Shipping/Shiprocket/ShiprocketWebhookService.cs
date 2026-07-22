using ecomm.api.Common.Tenancy;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Notifications;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Shipping.Shiprocket;

/// <summary>
/// Processes Shiprocket tracking webhooks (SR5). Cross-tenant: the shipment is found by AWB with
/// <c>IgnoreQueryFilters</c>, then all writes/notifications run inside that shipment's tenant scope.
/// Maps Shiprocket statuses onto our Shipment + Order lifecycle and records a timeline entry.
/// </summary>
public interface IShiprocketWebhookService
{
    Task<bool> HandleAsync(string awb, string shiprocketStatus, CancellationToken ct = default);

    /// <summary>Overload carrying the courier's extra scan detail, so nothing is lost.</summary>
    Task<bool> HandleAsync(string awb, string shiprocketStatus, ShiprocketScan scan, CancellationToken ct = default);
}

/// <summary>Optional detail from a tracking webhook beyond AWB + status.</summary>
public sealed record ShiprocketScan(string? Location, string? Remark, DateTime? OccurredAt, string? RawPayload);

public sealed class ShiprocketWebhookService(
    EcommerceDbContext db, ICurrentTenantService tenant, INotificationService notify, ILogger<ShiprocketWebhookService> log)
    : IShiprocketWebhookService
{
    public Task<bool> HandleAsync(string awb, string shiprocketStatus, CancellationToken ct = default)
        => HandleAsync(awb, shiprocketStatus, new ShiprocketScan(null, null, null, null), ct);

    public async Task<bool> HandleAsync(string awb, string shiprocketStatus, ShiprocketScan scan, CancellationToken ct = default)
    {
        var peek = await db.Shipments.IgnoreQueryFilters()
            .Where(s => s.TrackingNumber == awb && s.Provider == "Shiprocket")
            .Select(s => new { s.ShipmentId, s.TenantId })
            .FirstOrDefaultAsync(ct);
        if (peek is null) { log.LogInformation("Shiprocket webhook: no shipment for AWB {Awb}", awb); return false; }

        using (tenant.BeginScope(peek.TenantId))
        {
            var shipment = await db.Shipments.FirstAsync(s => s.ShipmentId == peek.ShipmentId, ct);
            var order = await db.Orders.FirstOrDefaultAsync(o => o.OrderId == shipment.OrderId, ct);
            var (shipStatus, orderStatus, delivered) = Map(shiprocketStatus);
            var now = DateTime.UtcNow;

            // Record the scan BEFORE deciding what to do with it. Unmapped statuses used to be
            // dropped entirely; the courier never resends them, so they were lost for good.
            db.ShipmentCheckpoints.Add(new ShipmentCheckpoint
            {
                ShipmentId = shipment.ShipmentId,
                RawStatus = Trim(shiprocketStatus, 150) ?? string.Empty,
                MappedStatus = shipStatus,
                Location = Trim(scan.Location, 150),
                Remark = Trim(scan.Remark, 300),
                OccurredAt = scan.OccurredAt,
                RawPayload = shipStatus is null ? Trim(scan.RawPayload, 4000) : null,
                CreatedAt = now,
            });
            if (shipStatus is null)
                log.LogInformation("Shiprocket AWB {Awb}: unmapped status '{Status}' recorded as a checkpoint", awb, shiprocketStatus);

            if (shipStatus is not null && shipment.Status != shipStatus)
            {
                shipment.Status = shipStatus;
                shipment.UpdatedAt = now;
                if (delivered) shipment.DeliveredAt = now;
            }

            var notifyDelivered = false;
            if (order is not null && orderStatus is not null
                && order.Status != orderStatus && order.Status is not ("Cancelled" or "Returned"))
            {
                var from = order.Status;
                order.Status = orderStatus;
                order.UpdatedAt = now;
                db.OrderStatusHistories.Add(new OrderStatusHistory
                {
                    OrderId = order.OrderId, FromStatus = from, ToStatus = orderStatus,
                    Notes = $"Shiprocket: {shiprocketStatus}", CreatedAt = now,
                });
                notifyDelivered = delivered;
            }

            await db.SaveChangesAsync(ct);
            if (notifyDelivered && order is not null) await NotifyAsync(order, ct);
            return true;
        }
    }

    /// <summary>Map a Shiprocket status string onto (our shipment status, our order status, is-delivered).</summary>
    private static (string? ship, string? order, bool delivered) Map(string status)
    {
        var v = (status ?? "").Trim().ToLowerInvariant();
        if (v.Contains("delivered") && !v.Contains("rto")) return ("Delivered", "Delivered", true);
        if (v.Contains("rto") || v.Contains("return")) return ("Returned", "Returned", false);
        if (v.Contains("cancel")) return ("Returned", null, false);
        if (v.Contains("out for delivery") || v.Contains("transit") || v.Contains("picked")
            || v.Contains("shipped") || v.Contains("dispatch")) return ("InTransit", "Shipped", false);
        return (null, null, false);   // unknown/intermediate — no lifecycle change, but still logged as a checkpoint
    }

    private static string? Trim(string? v, int max)
    {
        if (string.IsNullOrWhiteSpace(v)) return null;
        var t = v.Trim();
        return t.Length <= max ? t : t[..max];
    }

    private async Task NotifyAsync(Order order, CancellationToken ct)
    {
        try
        {
            var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == order.UserId, ct);
            if (string.IsNullOrWhiteSpace(user?.Email)) return;
            var tokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["CustomerName"] = user.FullName ?? "there",
                ["OrderNumber"] = order.OrderNumber,
                ["Status"] = "Delivered",
            };
            await notify.SendEmailAsync("OrderStatusUpdate", user.Email!, tokens, ct);
        }
        catch (Exception ex) { log.LogWarning(ex, "Shiprocket delivered-notification failed for order {Order}", order.OrderId); }
    }
}
