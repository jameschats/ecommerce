using ecomm.api.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Settings;

public interface INumberSequenceService
{
    Task<long> NextOrderSeqAsync(long tenantId, CancellationToken ct = default);
    Task<long> NextInvoiceSeqAsync(long tenantId, CancellationToken ct = default);
}

/// <summary>
/// Per-tenant order/invoice display numbers. OrderNumber/InvoiceNumber can't be derived from
/// Orders.OrderId/Invoices.InvoiceId (a single AUTO_INCREMENT shared across every tenant) if
/// each merchant is supposed to see their own numbering restart at 1 after a Go Live reset —
/// resetting the shared AUTO_INCREMENT would corrupt every other tenant's numbers.
///
/// Uses MySQL's `LAST_INSERT_ID(expr)` idiom for an atomic increment-and-fetch on a plain
/// column: the UPDATE takes an InnoDB row lock on that Tenants row for its duration, so two
/// concurrent order placements for the same tenant can never be handed the same number — the
/// same safety property a real AUTO_INCREMENT gives, just scoped per tenant instead of global.
/// </summary>
public sealed class NumberSequenceService : INumberSequenceService
{
    private readonly EcommerceDbContext _db;

    public NumberSequenceService(EcommerceDbContext db) => _db = db;

    public Task<long> NextOrderSeqAsync(long tenantId, CancellationToken ct = default) =>
        NextAsync("NextOrderSeq", tenantId, ct);

    public Task<long> NextInvoiceSeqAsync(long tenantId, CancellationToken ct = default) =>
        NextAsync("NextInvoiceSeq", tenantId, ct);

    private async Task<long> NextAsync(string column, long tenantId, CancellationToken ct)
    {
        // Built via concatenation, not string interpolation, so the {0} placeholder stays a real
        // ExecuteSqlRawAsync parameter (tenantId never becomes part of the SQL text) — column is
        // one of the two hardcoded literals above, never caller-supplied.
        var sql = "UPDATE `Tenants` SET `" + column + "` = LAST_INSERT_ID(`" + column + "` + 1) WHERE `TenantId` = {0}";
        await _db.Database.ExecuteSqlRawAsync(sql, [tenantId], ct);
        // SqlQueryRaw<T> for a scalar type wraps the query as `SELECT s.Value FROM (<sql>) AS s` —
        // it requires the inner query's column to literally be named "Value", which an unaliased
        // function call is not (MySQL names it "LAST_INSERT_ID()"). Verified against real MySQL only
        // via the raw mysql CLI before shipping, which never exercises this EF wrapping — that gap is
        // exactly how this broke every checkout in production the first time a real order was placed.
        return await _db.Database.SqlQueryRaw<long>("SELECT LAST_INSERT_ID() AS Value").SingleAsync(ct);
    }
}
