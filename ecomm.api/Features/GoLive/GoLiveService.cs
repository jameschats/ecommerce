using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Inventory;
using ecomm.api.Features.Settings;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.GoLive;

public interface IGoLiveService
{
    Task<GoLiveSummaryDto> GetSummaryAsync(CancellationToken ct = default);
    Task<GoLiveResetResultDto> ResetAsync(GoLiveResetRequest req, long? currentUserId, CancellationToken ct = default);
    Task MarkLiveAsync(CancellationToken ct = default);
}

/// <summary>
/// "Go live" data reset (Settings → Go live): clears everything a merchant recorded while testing
/// — orders, payments, invoices, notifications, sessions — so the store opens with a clean ledger.
/// Catalogue/settings/pages/contacts are never touched by the "always removed" set; a few more
/// categories (customers, traffic, import history, contact messages) are opt-in.
///
/// Order matters throughout <see cref="ResetAsync"/>: inventory release/restock must run before any
/// row is deleted (a cascade delete never executes business logic), and Reviews/CreditNotes are
/// deleted explicitly rather than relying on their Order FK (which is ON DELETE SET NULL, not
/// CASCADE — an order delete alone would silently orphan them, not remove them).
/// </summary>
public sealed class GoLiveService : IGoLiveService
{
    private const string ConfirmationPhrase = "DELETE ALL TRANSACTIONS";

    private long Tenant => _db.CurrentTenantId;
    private readonly EcommerceDbContext _db;
    private readonly IInventoryService _inventory;
    private readonly Features.Catalog.Services.IBundleService _bundles;

    public GoLiveService(EcommerceDbContext db, IInventoryService inventory, Features.Catalog.Services.IBundleService bundles)
    {
        _db = db;
        _inventory = inventory;
        _bundles = bundles;
    }

    public async Task<GoLiveSummaryDto> GetSummaryAsync(CancellationToken ct = default)
    {
        var tenant = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.TenantId == Tenant, ct)
            ?? throw new AppException("Tenant not found.", 404);

        var orders = await _db.Orders.AsNoTracking().Select(o => new { o.OrderId, o.TotalAmount }).ToListAsync(ct);
        var orderIds = orders.Select(o => o.OrderId).ToList();

        var orderLines = await _db.OrderItems.CountAsync(i => orderIds.Contains(i.OrderId), ct);
        var statusHistory = await _db.OrderStatusHistories.CountAsync(h => orderIds.Contains(h.OrderId), ct);
        var invoices = await _db.Invoices.CountAsync(ct);
        var payments = await _db.Payments.CountAsync(ct);
        var shipments = await _db.Shipments.CountAsync(ct);
        var reviews = await _db.Reviews.CountAsync(ct);
        var creditNotes = await _db.CreditNotes.CountAsync(ct);
        var inventoryIds = await _db.Inventory.Select(i => i.InventoryId).ToListAsync(ct);
        var stockMovement = await _db.InventoryTransactions.CountAsync(t => inventoryIds.Contains(t.InventoryId), ct);
        var carts = await _db.Carts.CountAsync(ct);
        var wishlists = await _db.WishlistItems.CountAsync(ct);
        var notifications = await _db.Notifications.CountAsync(ct);
        var notificationHistory = await _db.NotificationHistory.CountAsync(ct);
        var otps = await _db.OtpVerifications.CountAsync(ct);
        var tenantUserIds = await _db.Users.Select(u => u.UserId).ToListAsync(ct);
        var refreshTokens = await _db.RefreshTokens.CountAsync(t => tenantUserIds.Contains(t.UserId), ct);
        var backupCodes = await _db.UserTwoFactorBackupCodes.CountAsync(c => tenantUserIds.Contains(c.UserId), ct);

        var reservedUnits = await _db.Inventory.Where(i => i.ReservedQty > 0).SumAsync(i => (int?)i.ReservedQty, ct) ?? 0;
        var productsWithReserved = await _db.Inventory.CountAsync(i => i.ReservedQty > 0, ct);

        var customerUserIds = await CustomerUserIdsAsync(null, ct);
        var customerAddresses = await _db.CustomerAddresses.CountAsync(a => customerUserIds.Contains(a.UserId), ct);
        var traffic = await _db.CustomerEvents.CountAsync(ct) + await _db.PopularSearches.CountAsync(ct) + await _db.SearchLogs.CountAsync(ct);
        var importJobs = await _db.ImportJobs.CountAsync(ct);
        var contactMessages = await _db.ContactMessages.CountAsync(ct);

        return new GoLiveSummaryDto(
            IsLive: tenant.GoneLiveAt is not null, GoneLiveAt: tenant.GoneLiveAt,
            Orders: orders.Count, OrdersWorth: orders.Sum(o => o.TotalAmount),
            OrderLinesAndStatusHistory: orderLines + statusHistory,
            Invoices: invoices, Payments: payments, Shipments: shipments,
            ReviewsAndCreditNotes: reviews + creditNotes, StockMovementHistory: stockMovement,
            CartsAndWishlists: carts + wishlists, Notifications: notifications + notificationHistory,
            SignInSessionsAndOtps: otps + refreshTokens + backupCodes,
            ReservedUnits: reservedUnits, ProductsWithReservedUnits: productsWithReserved,
            NextInvoicePreview: $"INV-{DateTime.UtcNow:yyyy}-{tenant.NextInvoiceSeq:D5}",
            CustomerAccounts: customerUserIds.Count, CustomerAddresses: customerAddresses,
            TrafficAndSearchHistory: traffic, ImportJobs: importJobs, ContactMessages: contactMessages);
    }

    public async Task<GoLiveResetResultDto> ResetAsync(GoLiveResetRequest req, long? currentUserId, CancellationToken ct = default)
    {
        if (!string.Equals(req.ConfirmationText?.Trim(), ConfirmationPhrase, StringComparison.Ordinal))
            throw new AppException($"Type {ConfirmationPhrase} to confirm.");

        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.TenantId == Tenant, ct)
            ?? throw new AppException("Tenant not found.", 404);
        if (tenant.GoneLiveAt is not null)
            throw new AppException("This store is already live — the reset screen is permanently closed.", StatusCodes.Status409Conflict);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        // Every step below deletes explicitly by id set rather than relying on cascade (DB-level FK
        // cascade, or EF's own navigation-based cascade) — same convention AiCatalogService.ClearAsync
        // already uses. Two reasons: it works identically regardless of provider (this is exercised
        // against EF's InMemory provider in tests, which doesn't execute real SQL FK constraints), and
        // it keeps the actual deletion scope fully explicit and auditable in one place.

        // 1. Release/restock inventory BEFORE anything is deleted — deleting rows never runs this
        // business logic, so skipping this step would leave stock permanently stuck as reserved/committed.
        // Draft orders never held inventory (DraftOrderService.DeleteAsync's own convention); Cancelled/
        // Returned orders already had their stock corrected at the time they were cancelled/returned —
        // redoing it here would double-count.
        var orders = await _db.Orders.ToListAsync(ct);
        var orderIds = orders.Select(o => o.OrderId).ToList();
        var allItems = await _db.OrderItems.Where(i => orderIds.Contains(i.OrderId)).ToListAsync(ct);
        var itemsByOrder = allItems.ToLookup(i => i.OrderId);
        var reservedUnitsReleased = 0;
        foreach (var order in orders)
        {
            if (order.Status is "Draft" or "Cancelled" or "Returned") continue;
            var wasCommitted = order.Status is "Paid" or "Packed" or "Confirmed" or "Shipped" or "Delivered";
            foreach (var it in itemsByOrder[order.OrderId])
            {
                foreach (var (pid, vid, qty) in await _bundles.ExpandForInventoryAsync(it.ProductId, it.ProductVariantId, it.Quantity, ct))
                {
                    if (wasCommitted) await _inventory.RestockAsync(pid, vid, qty, "GoLiveReset", order.OrderId, ct);
                    else await _inventory.ReleaseAsync(pid, vid, qty, "GoLiveReset", order.OrderId, ct);
                    reservedUnitsReleased += qty;
                }
            }
        }

        // 2. Reviews / CreditNotes (+ CreditNoteItems) — explicit: their Order FK is ON DELETE SET
        // NULL, not CASCADE, so an order delete alone would silently orphan them, not remove them.
        var reviews = await _db.Reviews.ToListAsync(ct);
        var creditNotes = await _db.CreditNotes.ToListAsync(ct);
        var creditNoteIds = creditNotes.Select(c => c.CreditNoteId).ToList();
        var creditNoteItems = await _db.CreditNoteItems.Where(i => creditNoteIds.Contains(i.CreditNoteId)).ToListAsync(ct);
        var reviewsAndCreditNotesRemoved = reviews.Count + creditNotes.Count;
        _db.CreditNoteItems.RemoveRange(creditNoteItems);
        _db.CreditNotes.RemoveRange(creditNotes);
        _db.Reviews.RemoveRange(reviews);

        // 3. Payments (+PaymentTransactions/Refunds), Shipments (+ShipmentCheckpoints), Invoices
        // (+InvoiceItems), CouponUsage — all order-children, none relied on to cascade.
        var payments = await _db.Payments.Where(p => orderIds.Contains(p.OrderId)).ToListAsync(ct);
        var paymentIds = payments.Select(p => p.PaymentId).ToList();
        _db.PaymentTransactions.RemoveRange(await _db.PaymentTransactions.Where(t => paymentIds.Contains(t.PaymentId)).ToListAsync(ct));
        _db.Refunds.RemoveRange(await _db.Refunds.Where(r => orderIds.Contains(r.OrderId)).ToListAsync(ct));
        _db.Payments.RemoveRange(payments);

        var shipments = await _db.Shipments.Where(s => orderIds.Contains(s.OrderId)).ToListAsync(ct);
        var shipmentIds = shipments.Select(s => s.ShipmentId).ToList();
        _db.ShipmentCheckpoints.RemoveRange(await _db.ShipmentCheckpoints.Where(c => shipmentIds.Contains(c.ShipmentId)).ToListAsync(ct));
        _db.Shipments.RemoveRange(shipments);

        var invoices = await _db.Invoices.Where(i => orderIds.Contains(i.OrderId)).ToListAsync(ct);
        var invoiceIds = invoices.Select(i => i.InvoiceId).ToList();
        _db.InvoiceItems.RemoveRange(await _db.InvoiceItems.Where(i => invoiceIds.Contains(i.InvoiceId)).ToListAsync(ct));
        _db.Invoices.RemoveRange(invoices);

        _db.CouponUsages.RemoveRange(await _db.CouponUsages.Where(c => orderIds.Contains(c.OrderId)).ToListAsync(ct));

        var invoicesRemoved = invoices.Count;
        var paymentsRemoved = payments.Count;
        var shipmentsRemoved = shipments.Count;

        // 4. Orders (+ OrderItems + OrderStatusHistory).
        _db.OrderStatusHistories.RemoveRange(await _db.OrderStatusHistories.Where(h => orderIds.Contains(h.OrderId)).ToListAsync(ct));
        _db.OrderItems.RemoveRange(allItems);
        _db.Orders.RemoveRange(orders);

        await _db.SaveChangesAsync(ct);

        // 5. Stock movement LOG only — the actual Inventory.AvailableQty/ReservedQty balances were
        // already corrected in step 1 and are left as-is; this clears the audit trail of how they
        // got there (including pre-launch manual stock entry/adjustments, not just order-driven rows).
        var inventoryIds = await _db.Inventory.Select(i => i.InventoryId).ToListAsync(ct);
        var stockMovement = await _db.InventoryTransactions.Where(t => inventoryIds.Contains(t.InventoryId)).ToListAsync(ct);
        var stockMovementRemoved = stockMovement.Count;
        _db.InventoryTransactions.RemoveRange(stockMovement);

        // 6. Carts (+CartItems) & Wishlists.
        var carts = await _db.Carts.ToListAsync(ct);
        var cartIds = carts.Select(c => c.CartId).ToList();
        var wishlists = await _db.WishlistItems.ToListAsync(ct);
        var cartsAndWishlistsRemoved = carts.Count + wishlists.Count;
        _db.CartItems.RemoveRange(await _db.CartItems.Where(i => cartIds.Contains(i.CartId)).ToListAsync(ct));
        _db.Carts.RemoveRange(carts);
        _db.WishlistItems.RemoveRange(wishlists);

        // 7. Notifications (bell feed + send history).
        var notifications = await _db.Notifications.ToListAsync(ct);
        var notificationHistory = await _db.NotificationHistory.ToListAsync(ct);
        var notificationsRemoved = notifications.Count + notificationHistory.Count;
        _db.Notifications.RemoveRange(notifications);
        _db.NotificationHistory.RemoveRange(notificationHistory);

        await _db.SaveChangesAsync(ct);

        // 8. Sign-in sessions & OTPs. RefreshToken/UserTwoFactorBackupCode aren't tenant-scoped
        // themselves — join through this tenant's Users.
        var tenantUserIds = await _db.Users.Select(u => u.UserId).ToListAsync(ct);
        var otps = await _db.OtpVerifications.ToListAsync(ct);
        var refreshTokens = await _db.RefreshTokens.Where(t => tenantUserIds.Contains(t.UserId)).ToListAsync(ct);
        var backupCodes = await _db.UserTwoFactorBackupCodes.Where(c => tenantUserIds.Contains(c.UserId)).ToListAsync(ct);
        var sessionsRemoved = otps.Count + refreshTokens.Count + backupCodes.Count;
        _db.OtpVerifications.RemoveRange(otps);
        _db.RefreshTokens.RemoveRange(refreshTokens);
        _db.UserTwoFactorBackupCodes.RemoveRange(backupCodes);

        // 9. Reset per-tenant order/invoice numbering — this is what makes the summary's "your next
        // invoice will be low again" preview true afterward.
        tenant.NextOrderSeq = 1;
        tenant.NextInvoiceSeq = 1;
        tenant.UpdatedAt = DateTime.UtcNow;

        // 10-13. Optional categories.
        var customersRemoved = 0;
        if (req.IncludeCustomers)
        {
            var customerIds = await CustomerUserIdsAsync(currentUserId, ct);
            _db.CustomerAddresses.RemoveRange(await _db.CustomerAddresses.Where(a => customerIds.Contains(a.UserId)).ToListAsync(ct));
            _db.CustomerProfiles.RemoveRange(await _db.CustomerProfiles.Where(p => customerIds.Contains(p.UserId)).ToListAsync(ct));
            var customers = await _db.Users.Where(u => customerIds.Contains(u.UserId)).ToListAsync(ct);
            customersRemoved = customers.Count;
            _db.Users.RemoveRange(customers);
        }

        var trafficRemoved = 0;
        if (req.IncludeTraffic)
        {
            var events = await _db.CustomerEvents.ToListAsync(ct);
            var searches = await _db.PopularSearches.ToListAsync(ct);
            var searchLogs = await _db.SearchLogs.ToListAsync(ct);
            trafficRemoved = events.Count + searches.Count + searchLogs.Count;
            _db.CustomerEvents.RemoveRange(events);
            _db.PopularSearches.RemoveRange(searches);
            _db.SearchLogs.RemoveRange(searchLogs);
        }

        var importJobsRemoved = 0;
        if (req.IncludeImportHistory)
        {
            var jobs = await _db.ImportJobs.ToListAsync(ct);
            var jobIds = jobs.Select(j => j.ImportJobId).ToList();
            importJobsRemoved = jobs.Count;
            _db.ImportJobItems.RemoveRange(await _db.ImportJobItems.Where(i => jobIds.Contains(i.ImportJobId)).ToListAsync(ct));
            _db.ImportJobs.RemoveRange(jobs);
        }

        var contactMessagesRemoved = 0;
        if (req.IncludeContactMessages)
        {
            var contacts = await _db.ContactMessages.ToListAsync(ct);
            contactMessagesRemoved = contacts.Count;
            _db.ContactMessages.RemoveRange(contacts);
        }

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new GoLiveResetResultDto(
            OrdersRemoved: orderIds.Count, InvoicesRemoved: invoicesRemoved, PaymentsRemoved: paymentsRemoved,
            ShipmentsRemoved: shipmentsRemoved, ReviewsAndCreditNotesRemoved: reviewsAndCreditNotesRemoved,
            StockMovementRemoved: stockMovementRemoved, CartsAndWishlistsRemoved: cartsAndWishlistsRemoved,
            NotificationsRemoved: notificationsRemoved, SignInSessionsAndOtpsRemoved: sessionsRemoved,
            ReservedUnitsReleased: reservedUnitsReleased,
            CustomersRemoved: customersRemoved, TrafficRemoved: trafficRemoved,
            ImportJobsRemoved: importJobsRemoved, ContactMessagesRemoved: contactMessagesRemoved);
    }

    public async Task MarkLiveAsync(CancellationToken ct = default)
    {
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.TenantId == Tenant, ct)
            ?? throw new AppException("Tenant not found.", 404);
        if (tenant.GoneLiveAt is not null)
            throw new AppException("This store is already live.", StatusCodes.Status409Conflict);

        tenant.GoneLiveAt = DateTime.UtcNow;
        tenant.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Customer-role users eligible for deletion: excludes the signed-in admin and anyone
    /// holding any role other than Customer (Staff/Admin accounts are always kept).</summary>
    private async Task<List<long>> CustomerUserIdsAsync(long? excludeUserId, CancellationToken ct) =>
        await _db.Users
            .Where(u => excludeUserId == null || u.UserId != excludeUserId)
            .Where(u => !u.UserRoles.Any(ur => ur.Role != null && ur.Role.NormalizedName != "CUSTOMER"))
            .Select(u => u.UserId)
            .ToListAsync(ct);
}
