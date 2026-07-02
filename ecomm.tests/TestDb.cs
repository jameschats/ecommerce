using ecomm.api.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace ecomm.tests;

/// <summary>Builds an isolated in-memory EcommerceDbContext per test.</summary>
public static class TestDb
{
    public static EcommerceDbContext New()
    {
        var options = new DbContextOptionsBuilder<EcommerceDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .EnableSensitiveDataLogging()
            .Options;
        return new EcommerceDbContext(options);
    }
}
