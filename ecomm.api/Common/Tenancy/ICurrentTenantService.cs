namespace ecomm.api.Common.Tenancy;

/// <summary>
/// Resolves the tenant for the current scope. Backed by the request (set by
/// <see cref="TenantResolutionMiddleware"/>), an explicit ambient override
/// (background jobs / seeders), or the configured default tenant.
/// </summary>
public interface ICurrentTenantService
{
    /// <summary>The tenant id in effect for this scope. Never throws — falls back to the default tenant.</summary>
    long CurrentTenantId { get; }

    /// <summary>True when a real tenant was resolved from the request (vs. the default fallback).</summary>
    bool IsResolved { get; }

    /// <summary>
    /// Force a tenant for the current async flow (background jobs, seeders, tests).
    /// Returns an IDisposable that restores the previous value.
    /// </summary>
    IDisposable BeginScope(long tenantId);
}
