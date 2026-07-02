using ecomm.api.Data.Context;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ecomm.api.Common.Health;

/// <summary>Readiness probe: reports Unhealthy if the database can't be reached.</summary>
public sealed class DatabaseHealthCheck : IHealthCheck
{
    private readonly EcommerceDbContext _db;

    public DatabaseHealthCheck(EcommerceDbContext db) => _db = db;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _db.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Database unreachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database error.", ex);
        }
    }
}
