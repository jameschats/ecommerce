using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Settings;

/// <summary>
/// What a reset would remove. The screen shows every number before anything is touched —
/// the whole point of the feature is that nobody has to guess.
/// </summary>
public sealed record ResetPreview(
    bool IsLive, DateTime? LiveSince,
    int Orders, decimal OrderValue, int OrderItems, int StatusHistory,
    int Invoices, int Payments, int Shipments, int Reviews, int CreditNotes,
    int InventoryTransactions, int ReservedUnits, int ProductsHoldingStock,
    int Notifications, int Carts, int Sessions, int Otps, int Wishlist,
    int CustomerAccounts, int Addresses,
    int PageViews, int SearchLogs, int ImportJobs, int Contacts,
    int InvoiceCounter, int OrderCounter);

/// <summary>The optional groups. Everything not listed here goes unconditionally.</summary>
public sealed record ResetOptions(
    bool CustomerAccounts = false,
    bool Traffic = false,
    bool ImportHistory = false,
    bool Contacts = false);

public sealed record ResetResult(ResetPreview Removed, string[] Notes);

public interface IDataResetService
{
    Task<ResetPreview> PreviewAsync(CancellationToken ct = default);
    Task<ResetResult> ResetAsync(string confirm, ResetOptions options, long? actingUserId, CancellationToken ct = default);
    Task<DateTime> GoLiveAsync(CancellationToken ct = default);
}

/// <summary>
/// Clears the shop's trading history so it can open with a clean ledger, keeping the catalogue,
/// the settings and everything else that took work to build.
///
/// This exists because the alternative — running DELETE statements by hand at go-live — gets
/// three things wrong every time, and all three are silent:
///
///   1. Stock stays reserved. Reserving moves units from AvailableQty to ReservedQty; the
///      foreign keys cascade the order away but never touch Inventory, so the reservation is
///      stranded and the shop reads permanently short.
///   2. Invoice numbers carry on. They are derived from the primary key, and MySQL does not
///      roll AUTO_INCREMENT back on delete, so the first real customer would be handed invoice
///      number seven.
///   3. Reviews and credit notes outlive their orders — those two foreign keys are SET NULL,
///      not CASCADE, so a review would quietly survive as an unverified one.
///
/// Once the shop is live this becomes unavailable, permanently: see <see cref="GoLiveAsync"/>.
/// </summary>
public sealed class DataResetService : IDataResetService
{
    private const long Tenant = 1;

    /// <summary>Typed in full, in capitals. Long enough that nobody arrives here by accident.</summary>
    public const string ConfirmPhrase = "DELETE ALL TRANSACTIONS";

    /// <summary>Stamped when the shop opens for real. Its presence is the lock.</summary>
    public const string LiveSinceKey = "Store.LiveSince";

    private readonly EcommerceDbContext _db;
    private readonly ILogger<DataResetService> _log;

    public DataResetService(EcommerceDbContext db, ILogger<DataResetService> log)
    {
        _db = db;
        _log = log;
    }

    // ------------------------------------------------------------------ preview

    public async Task<ResetPreview> PreviewAsync(CancellationToken ct = default)
    {
        var liveSince = await LiveSinceAsync(ct);

        var inventory = await _db.Inventory
            .Where(i => i.ReservedQty > 0)
            .Select(i => i.ReservedQty)
            .ToListAsync(ct);

        var customers = await CustomerIdsAsync(null, ct);
        var counters = await CountersAsync(ct);

        return new ResetPreview(
            IsLive: liveSince is not null,
            LiveSince: liveSince,
            Orders: await CountAsync("Orders", ct),
            OrderValue: await _db.Orders.SumAsync(o => (decimal?)o.TotalAmount, ct) ?? 0m,
            OrderItems: await CountAsync("OrderItems", ct),
            StatusHistory: await CountAsync("OrderStatusHistory", ct),
            Invoices: await CountAsync("Invoices", ct),
            Payments: await CountAsync("Payments", ct),
            Shipments: await CountAsync("Shipments", ct),
            Reviews: await CountAsync("Reviews", ct),
            CreditNotes: await CountAsync("CreditNotes", ct),
            InventoryTransactions: await CountAsync("InventoryTransactions", ct),
            ReservedUnits: inventory.Sum(),
            ProductsHoldingStock: inventory.Count,
            Notifications: await CountAsync("Notifications", ct) + await CountAsync("NotificationHistory", ct),
            Carts: await CountAsync("Cart", ct),
            Sessions: await CountAsync("RefreshTokens", ct),
            Otps: await CountAsync("OtpVerifications", ct),
            Wishlist: await CountAsync("WishlistItems", ct),
            CustomerAccounts: customers.Count,
            Addresses: await CountAsync("CustomerAddresses", ct),
            PageViews: await CountAsync("PageViews", ct),
            SearchLogs: await CountAsync("SearchLogs", ct) + await CountAsync("PopularSearches", ct),
            ImportJobs: await CountAsync("ImportJobs", ct),
            Contacts: await CountAsync("Contacts", ct),
            InvoiceCounter: counters.Invoice,
            OrderCounter: counters.Order);
    }

    // -------------------------------------------------------------------- reset

    public async Task<ResetResult> ResetAsync(
        string confirm, ResetOptions options, long? actingUserId, CancellationToken ct = default)
    {
        // Both guards before the preview, which is twenty-odd count queries. A refused call
        // should cost one read, and nothing should touch the tables until both have passed.
        var liveSince = await LiveSinceAsync(ct);
        if (liveSince is not null)
            throw new AppException(
                $"This shop went live on {liveSince:d MMMM yyyy}. Trading records cannot be "
                + "bulk-deleted from admin any more — remove individual orders instead, or restore "
                + "from a backup if something has genuinely gone wrong.",
                StatusCodes.Status409Conflict);

        if (!string.Equals(confirm?.Trim(), ConfirmPhrase, StringComparison.Ordinal))
            throw new AppException($"Type {ConfirmPhrase} exactly, in capitals, to confirm.");

        var before = await PreviewAsync(ct);

        // Which customers go, decided before the transaction so the acting admin is never in
        // the list even if somebody has given them a Customer role alongside their staff one.
        var customerIds = options.CustomerAccounts
            ? await CustomerIdsAsync(actingUserId, ct)
            : new List<long>();

        var notes = new List<string>();

        await using (var tx = await _db.Database.BeginTransactionAsync(ct))
        {
            // Reviews and credit notes first. Both point at Orders with SET NULL, so a cascade
            // would leave them behind rather than take them with it.
            await ExecAsync("DELETE FROM Reviews", ct);
            await ExecAsync("DELETE FROM CreditNotes", ct);

            // Orders carry the rest down with them: items, status history, invoices and their
            // items, payments and their transactions, refunds, shipments, coupon usage.
            await ExecAsync("DELETE FROM Orders", ct);

            await ExecAsync("DELETE FROM InventoryTransactions", ct);

            // Hand the reservations back. Nothing else does this — the orders that were holding
            // the stock are gone, and Inventory has no foreign key to them to cascade through.
            var released = await ExecAsync(
                "UPDATE Inventory SET AvailableQty = AvailableQty + ReservedQty, ReservedQty = 0 "
                + "WHERE ReservedQty > 0", ct);
            if (released > 0)
                notes.Add($"Released {before.ReservedUnits} reserved unit(s) back to available stock "
                          + $"across {released} product(s). Committed sales are not reversed — confirm "
                          + "your opening stock on the Inventory screen before you open.");

            await ExecAsync("DELETE FROM Cart", ct);              // CartItems cascade
            await ExecAsync("DELETE FROM WishlistItems", ct);
            await ExecAsync("DELETE FROM Notifications", ct);
            await ExecAsync("DELETE FROM NotificationHistory", ct);
            await ExecAsync("DELETE FROM AuditLogs", ct);

            // Everyone is signed out. Access tokens stay valid until they expire, so this call
            // still returns normally to the admin who made it.
            await ExecAsync("DELETE FROM RefreshTokens", ct);
            await ExecAsync("DELETE FROM OtpVerifications", ct);
            notes.Add("All sessions ended — customers and staff will sign in again.");

            if (options.Traffic)
            {
                await ExecAsync("DELETE FROM PageViews", ct);
                await ExecAsync("DELETE FROM SearchLogs", ct);
                await ExecAsync("DELETE FROM PopularSearches", ct);
            }

            if (options.ImportHistory)
                await ExecAsync("DELETE FROM ImportJobs", ct);    // ImportJobItems cascade

            if (options.Contacts)
                await ExecAsync("DELETE FROM Contacts", ct);

            if (customerIds.Count > 0)
            {
                // Addresses, external logins and role rows cascade from Users.
                var users = await _db.Users.Where(u => customerIds.Contains(u.UserId)).ToListAsync(ct);
                _db.Users.RemoveRange(users);
                await _db.SaveChangesAsync(ct);
                notes.Add($"Removed {users.Count} customer account(s). Staff accounts were kept.");
            }

            await tx.CommitAsync(ct);
        }

        // After the commit, deliberately. ALTER is DDL and forces an implicit commit in MySQL, so
        // running these inside the transaction above would end it early and quietly.
        var counters = new List<string>
        {
            "Orders", "OrderItems", "OrderStatusHistory", "Invoices", "InvoiceItems",
            "Payments", "PaymentTransactions", "Refunds", "Shipments", "CouponUsage",
            "CreditNotes", "CreditNoteItems", "Reviews", "InventoryTransactions",
            "Notifications", "NotificationHistory", "Cart", "CartItems", "WishlistItems",
            "AuditLogs", "RefreshTokens", "OtpVerifications",
        };
        if (options.Traffic) counters.AddRange(new[] { "PageViews", "SearchLogs", "PopularSearches" });
        if (options.ImportHistory) counters.AddRange(new[] { "ImportJobs", "ImportJobItems" });
        if (options.Contacts) counters.Add("Contacts");

        foreach (var table in counters)
            await ExecAsync($"ALTER TABLE {table} AUTO_INCREMENT = 1", ct);

        notes.Add("Numbering restarted — your first real invoice will be INV-"
                  + $"{DateTime.UtcNow:yyyy}-00001.");

        // Logged because the rows that would have shown what happened are the rows just deleted.
        _log.LogWarning(
            "DATA RESET by user {User}: {Orders} orders (value {Value}), {Invoices} invoices, "
            + "{Payments} payments, {Reserved} units unreserved, customers removed: {Customers}.",
            actingUserId, before.Orders, before.OrderValue, before.Invoices, before.Payments,
            before.ReservedUnits, customerIds.Count);

        return new ResetResult(before, notes.ToArray());
    }

    // ------------------------------------------------------------------ go live

    /// <summary>
    /// Closes the door. Stamped once and never cleared from admin: a switch that can be flicked
    /// back is not a safeguard. Undoing this needs someone at the database, which is the point.
    /// </summary>
    public async Task<DateTime> GoLiveAsync(CancellationToken ct = default)
    {
        var existing = await LiveSinceAsync(ct);
        if (existing is not null) return existing.Value;

        var now = DateTime.UtcNow;
        var row = await _db.Settings.FirstOrDefaultAsync(
            s => s.TenantId == Tenant && s.SettingKey == LiveSinceKey, ct);

        if (row is null)
            _db.Settings.Add(new Setting
            {
                TenantId = Tenant,
                SettingKey = LiveSinceKey,
                SettingValue = now.ToString("O"),
                DataType = "string",
                Category = "Store",
                CreatedAt = now,
            });
        else
            row.SettingValue = now.ToString("O");

        await _db.SaveChangesAsync(ct);
        _log.LogWarning("Shop marked live at {When}. Bulk data reset is now permanently disabled.", now);
        return now;
    }

    // ------------------------------------------------------------------ helpers

    private async Task<DateTime?> LiveSinceAsync(CancellationToken ct)
    {
        var value = await _db.Settings
            .Where(s => s.TenantId == Tenant && s.SettingKey == LiveSinceKey)
            .Select(s => s.SettingValue)
            .FirstOrDefaultAsync(ct);

        return DateTime.TryParse(value, null, System.Globalization.DateTimeStyles.RoundtripKind, out var when)
            ? when
            : null;
    }

    /// <summary>
    /// Customers, meaning users holding no role other than Customer. Anyone with a staff role
    /// is kept whatever else they hold, and so is the admin running the reset — locking yourself
    /// out mid-cleanup would be a poor end to it.
    /// </summary>
    public async Task<List<long>> CustomerIdsAsync(long? exclude, CancellationToken ct = default)
    {
        var staffIds = await (
            from ur in _db.UserRoles
            join r in _db.Roles on ur.RoleId equals r.RoleId
            where r.Name != "Customer"
            select ur.UserId).Distinct().ToListAsync(ct);

        return await _db.Users
            .Where(u => !staffIds.Contains(u.UserId) && (exclude == null || u.UserId != exclude))
            .Select(u => u.UserId)
            .ToListAsync(ct);
    }

    // A table name is an identifier, so it cannot be a parameter — it is interpolated. Every
    // caller passes a string literal from the fixed lists in this file; none of it reaches here
    // from a request. Hence the suppression rather than a rewrite.
#pragma warning disable EF1002
    private Task<int> CountAsync(string table, CancellationToken ct) =>
        _db.Database.SqlQueryRaw<int>($"SELECT COUNT(*) AS Value FROM {table}").SingleAsync(ct);
#pragma warning restore EF1002

    /// <summary>
    /// The value the next insert into <paramref name="table"/> will take, which is what invoice
    /// numbers are built from.
    ///
    /// The stats_expiry reset is not optional: MySQL 8 caches AUTO_INCREMENT in the data
    /// dictionary for a day by default, and ANALYZE TABLE does not refresh it. Without this the
    /// preview happily reports yesterday's counter — verified locally, where information_schema
    /// still read 6 for a table SHOW CREATE TABLE had already reset to 1.
    /// </summary>
    private async Task<(int Invoice, int Order)> CountersAsync(CancellationToken ct)
    {
        // The connection has to be held open across both statements. EF hands it back to the
        // pool after each command by default, and the session variable goes with it — which is
        // how the first attempt at this silently kept reading the cached value.
        await _db.Database.OpenConnectionAsync(ct);
        try
        {
            await _db.Database.ExecuteSqlRawAsync("SET SESSION information_schema_stats_expiry = 0", ct);
            return (await NextIdAsync("Invoices", ct), await NextIdAsync("Orders", ct));
        }
        finally
        {
            await _db.Database.CloseConnectionAsync();
        }
    }

    private async Task<int> NextIdAsync(string table, CancellationToken ct)
    {
        // SqlQuery, not SqlQueryRaw: here the table name is a value being compared, not an
        // identifier, so it goes in as a real parameter. One literal, not two concatenated —
        // concatenation would produce a string and lose the parameterisation.
        return await _db.Database.SqlQuery<int>(
            $"SELECT CAST(COALESCE(AUTO_INCREMENT, 1) AS SIGNED) AS Value FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = {table}")
            .SingleAsync(ct);
    }

    private Task<int> ExecAsync(string sql, CancellationToken ct) =>
        _db.Database.ExecuteSqlRawAsync(sql, ct);
}
