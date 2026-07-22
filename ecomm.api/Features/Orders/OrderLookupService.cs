using ecomm.api.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Orders;

/// <summary>
/// Deliberately narrow: enough to answer "where is my order", nothing more. No addresses, no
/// totals, no line items, no customer name — a lookup that only needs an order number and an
/// email must never become a way to read someone's purchase history.
/// </summary>
public sealed record OrderLookupDto(
    string orderNumber, string status, DateTime? placedAt,
    string? courier, string? trackingNumber, DateTime? estimatedDeliveryDate,
    DateTime? shippedAt, DateTime? deliveredAt,
    IReadOnlyList<OrderTimelineEntryDto> timeline);

public interface IOrderLookupService
{
    Task<OrderLookupDto?> FindAsync(string orderNumber, string email, CancellationToken ct = default);
}

/// <summary>
/// Guest "track my order" lookup. Checkout is member-only today, so everyone with an order has an
/// account — but making them remember a password to ask where their parcel is drives them into the
/// support queue instead. This is the self-serve path.
///
/// Order numbers are ORD{yyyyMMdd}-{id:D5}, i.e. trivially enumerable, so the email is the only
/// real secret. The service therefore returns <c>null</c> identically for "no such order" and
/// "email doesn't match" — the caller must not be able to tell those apart, or it becomes an
/// oracle for which order numbers exist.
/// </summary>
public sealed class OrderLookupService(EcommerceDbContext db) : IOrderLookupService
{
    public async Task<OrderLookupDto?> FindAsync(string orderNumber, string email, CancellationToken ct = default)
    {
        orderNumber = (orderNumber ?? string.Empty).Trim();
        email = (email ?? string.Empty).Trim();
        if (orderNumber.Length == 0 || email.Length == 0) return null;

        // One query, both conditions — no separate "does the order exist" step to leak timing.
        var match = await (
            from o in db.Orders.AsNoTracking()
            join u in db.Users.AsNoTracking() on o.UserId equals u.UserId
            where o.OrderNumber == orderNumber
                  && u.NormalizedEmail == email.ToUpperInvariant()
                  && o.Status != "Draft"
                  && !o.IsTest
            select new { o.OrderId, o.OrderNumber, o.Status, o.PlacedAt }
        ).FirstOrDefaultAsync(ct);

        if (match is null) return null;

        var shipment = await db.Shipments.AsNoTracking()
            .Where(s => s.OrderId == match.OrderId)
            .OrderByDescending(s => s.ShipmentId)
            .Select(s => new { s.ShipmentId, s.Courier, s.TrackingNumber, s.EstimatedDeliveryDate, s.ShippedAt, s.DeliveredAt })
            .FirstOrDefaultAsync(ct);

        var timeline = new List<OrderTimelineEntryDto>();

        // Status changes only — Notes can carry internal wording, so it is not exposed here.
        timeline.AddRange(await db.OrderStatusHistories.AsNoTracking()
            .Where(h => h.OrderId == match.OrderId)
            .Select(h => new OrderTimelineEntryDto(h.ToStatus, null, null, h.CreatedAt, "order"))
            .ToListAsync(ct));

        if (shipment is not null)
            timeline.AddRange(await db.ShipmentCheckpoints.AsNoTracking()
                .Where(c => c.ShipmentId == shipment.ShipmentId)
                .Select(c => new OrderTimelineEntryDto(
                    c.MappedStatus ?? c.RawStatus, null, c.Location, c.OccurredAt ?? c.CreatedAt, "courier"))
                .ToListAsync(ct));

        return new OrderLookupDto(
            match.OrderNumber, match.Status, match.PlacedAt,
            shipment?.Courier, shipment?.TrackingNumber, shipment?.EstimatedDeliveryDate,
            shipment?.ShippedAt, shipment?.DeliveredAt,
            timeline.OrderBy(e => e.at).ToList());
    }
}
