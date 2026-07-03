using Microsoft.Extensions.Options;

namespace ecomm.api.Common.Tenancy;

public sealed class TenancyOptions
{
    public const string SectionName = "Tenancy";
    /// <summary>Fallback tenant for the apex domain and non-request contexts (startup, jobs). V1 = 1.</summary>
    public long DefaultTenantId { get; set; } = 1;
    /// <summary>Root domain, e.g. "calendarshop.online". A "{slug}.{BaseDomain}" host resolves that slug; the apex + www resolve to the default tenant. Empty = always default (single-tenant / dev).</summary>
    public string BaseDomain { get; set; } = string.Empty;
}

/// <summary>
/// Resolution order: request (HttpContext.Items["TenantId"], set by the middleware)
/// → explicit ambient override (BeginScope) → configured default tenant.
/// Fail-open to the default so startup seeding / health checks / jobs never break;
/// the middleware fail-CLOSES real multi-tenant hosts (unknown subdomain → 404).
/// </summary>
public sealed class CurrentTenantService : ICurrentTenantService
{
    internal const string HttpContextItemKey = "TenantId";

    private static readonly AsyncLocal<long?> _ambient = new();
    private readonly IHttpContextAccessor _http;
    private readonly long _defaultTenantId;

    public CurrentTenantService(IHttpContextAccessor http, IOptions<TenancyOptions> options)
    {
        _http = http;
        _defaultTenantId = options.Value.DefaultTenantId;
    }

    public long CurrentTenantId
    {
        get
        {
            if (_http.HttpContext?.Items.TryGetValue(HttpContextItemKey, out var v) == true && v is long id)
                return id;
            return _ambient.Value ?? _defaultTenantId;
        }
    }

    public bool IsResolved =>
        _http.HttpContext?.Items.ContainsKey(HttpContextItemKey) == true || _ambient.Value.HasValue;

    public IDisposable BeginScope(long tenantId)
    {
        var previous = _ambient.Value;
        _ambient.Value = tenantId;
        return new Restore(() => _ambient.Value = previous);
    }

    private sealed class Restore(Action dispose) : IDisposable
    {
        public void Dispose() => dispose();
    }
}
