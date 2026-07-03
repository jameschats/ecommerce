using ecomm.api.Common.Tenancy;
using ecomm.api.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace ecomm.tests;

/// <summary>Builds an isolated in-memory EcommerceDbContext per test.</summary>
public static class TestDb
{
    /// <summary>New in-memory context scoped to <paramref name="tenantId"/> (default 1).</summary>
    public static EcommerceDbContext New(long tenantId = 1)
    {
        var options = new DbContextOptionsBuilder<EcommerceDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .EnableSensitiveDataLogging()
            .Options;
        return new EcommerceDbContext(options, new FixedTenant(tenantId));
    }

    /// <summary>Shares one in-memory database across contexts (for cross-tenant isolation tests).</summary>
    public static EcommerceDbContext ForDatabase(string dbName, long tenantId)
    {
        var options = new DbContextOptionsBuilder<EcommerceDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .EnableSensitiveDataLogging()
            .Options;
        return new EcommerceDbContext(options, new FixedTenant(tenantId));
    }
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
