using ecomm.api.Common.Tenancy;
using ecomm.api.Data.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.InMemory.Internal;

namespace ecomm.tests;

/// <summary>Builds an isolated in-memory EcommerceDbContext per test.</summary>
public static class TestDb
{
    /// <summary>New in-memory context scoped to <paramref name="tenantId"/> (default 1).</summary>
    public static EcommerceDbContext New(long tenantId = 1)
    {
        var options = Build(Guid.NewGuid().ToString());
        return new EcommerceDbContext(options, new FixedTenant(tenantId));
    }

    /// <summary>
    /// Shared builder. Transactions are a no-op in the in-memory provider, which by default throws; services
    /// that wrap writes in one (e.g. draft-order convert) are otherwise untestable here. Suppressing the warning
    /// lets those paths run — but note it means <b>rollback behaviour is not exercised</b> by in-memory tests.
    /// </summary>
    private static DbContextOptions<EcommerceDbContext> Build(string dbName) =>
        new DbContextOptionsBuilder<EcommerceDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .EnableSensitiveDataLogging()
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

    /// <summary>Shares one in-memory database across contexts (for cross-tenant isolation tests).</summary>
    public static EcommerceDbContext ForDatabase(string dbName, long tenantId) =>
        ForDatabase(dbName, new FixedTenant(tenantId));

    /// <summary>Shared in-memory database with a caller-supplied tenant context (for BeginScope tests).</summary>
    public static EcommerceDbContext ForDatabase(string dbName, ICurrentTenantService tenant) =>
        new(Build(dbName), tenant);
}

/// <summary>Test tenant context returning a fixed (mutable) tenant id.</summary>
public sealed class FixedTenant(long id) : ICurrentTenantService
{
    public long CurrentTenantId { get; private set; } = id;
    public bool IsResolved => true;

    public IDisposable BeginScope(long tenantId)
    {
        var previous = CurrentTenantId;
        CurrentTenantId = tenantId;
        return new Restore(() => CurrentTenantId = previous);
    }

    private sealed class Restore(Action dispose) : IDisposable
    {
        public void Dispose() => dispose();
    }
}
