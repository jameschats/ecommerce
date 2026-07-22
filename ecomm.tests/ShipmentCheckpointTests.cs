using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Shipping.Shiprocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ecomm.tests;

/// <summary>
/// Courier tracking scans (C2). The behaviour that matters: an unrecognised status used to be
/// discarded outright, and couriers do not resend history — so the regression these lock in is
/// "every webhook leaves a trace, mapped or not".
/// </summary>
public class ShipmentCheckpointTests
{
    private static (EcommerceDbContext db, ShiprocketWebhookService svc) Setup()
    {
        var db = TestDb.New(tenantId: 1);
        db.Orders.Add(new Order
        {
            OrderId = 50, TenantId = 1, UserId = 5, OrderNumber = "ORD-50",
            Status = "Shipped", CreatedAt = DateTime.UtcNow,
        });
        db.Shipments.Add(new Shipment
        {
            ShipmentId = 9, TenantId = 1, OrderId = 50, Provider = "Shiprocket",
            TrackingNumber = "AWB123", Status = "InTransit", CreatedAt = DateTime.UtcNow,
        });
        db.SaveChanges();

        return (db, new ShiprocketWebhookService(db, new FixedTenant(1), new RecordingEmail(),
            NullLogger<ShiprocketWebhookService>.Instance));
    }

    [Fact]
    public async Task An_unmapped_status_is_still_recorded_with_its_raw_payload()
    {
        var (db, svc) = Setup();
        using var _ = db;

        var handled = await svc.HandleAsync("AWB123", "Reached destination hub",
            new ShiprocketScan("Coimbatore", "Bag scanned", new DateTime(2026, 7, 20, 8, 30, 0), "{\"awb\":\"AWB123\"}"));

        Assert.True(handled);
        var cp = await db.ShipmentCheckpoints.SingleAsync();
        Assert.Equal("Reached destination hub", cp.RawStatus);
        Assert.Null(cp.MappedStatus);                       // we don't understand it…
        Assert.Equal("Coimbatore", cp.Location);            // …but we keep everything about it
        Assert.Equal("Bag scanned", cp.Remark);
        Assert.Equal(new DateTime(2026, 7, 20, 8, 30, 0), cp.OccurredAt);
        Assert.False(string.IsNullOrEmpty(cp.RawPayload));  // kept, so the mapping can be improved later

        // And the lifecycle is untouched.
        Assert.Equal("InTransit", (await db.Shipments.SingleAsync()).Status);
        Assert.Equal("Shipped", (await db.Orders.SingleAsync()).Status);
    }

    [Fact]
    public async Task A_mapped_status_updates_the_lifecycle_and_records_the_scan()
    {
        var (db, svc) = Setup();
        using var _ = db;

        await svc.HandleAsync("AWB123", "Delivered", new ShiprocketScan("Chennai", null, null, "{}"));

        var cp = await db.ShipmentCheckpoints.SingleAsync();
        Assert.Equal("Delivered", cp.MappedStatus);
        Assert.Null(cp.RawPayload);   // only kept when we couldn't map it

        Assert.Equal("Delivered", (await db.Shipments.SingleAsync()).Status);
        Assert.Equal("Delivered", (await db.Orders.SingleAsync()).Status);
        Assert.NotNull((await db.Shipments.SingleAsync()).DeliveredAt);
    }

    [Fact]
    public async Task Successive_scans_accumulate_rather_than_overwrite()
    {
        var (db, svc) = Setup();
        using var _ = db;

        await svc.HandleAsync("AWB123", "Picked up", new ShiprocketScan("Erode", null, null, null));
        await svc.HandleAsync("AWB123", "In transit", new ShiprocketScan("Salem", null, null, null));
        await svc.HandleAsync("AWB123", "Out for delivery", new ShiprocketScan("Chennai", null, null, null));

        var scans = await db.ShipmentCheckpoints.OrderBy(c => c.ShipmentCheckpointId).ToListAsync();
        Assert.Equal(3, scans.Count);
        Assert.Equal(new[] { "Erode", "Salem", "Chennai" }, scans.Select(s => s.Location));
    }

    [Fact]
    public async Task An_unknown_awb_records_nothing()
    {
        var (db, svc) = Setup();
        using var _ = db;

        var handled = await svc.HandleAsync("AWB-NOPE", "Delivered");

        Assert.False(handled);
        Assert.Empty(await db.ShipmentCheckpoints.ToListAsync());
    }
}
