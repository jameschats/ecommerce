using ecomm.api.Common.Tenancy;
using Serilog.Core;
using Serilog.Events;

namespace ecomm.api.Common.Middleware;

/// <summary>
/// Stamps every log event (within a request) with CorrelationId + TenantId, so
/// logs are filterable per request and per store in Seq (design-v2.md V2-10).
/// Picked up automatically via Serilog's ReadFrom.Services.
/// </summary>
public sealed class HttpContextLogEnricher(IHttpContextAccessor accessor) : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory factory)
    {
        var items = accessor.HttpContext?.Items;
        if (items is null) return;

        if (items.TryGetValue(CorrelationIdMiddleware.ItemKey, out var cid) && cid is string s)
            logEvent.AddPropertyIfAbsent(factory.CreateProperty("CorrelationId", s));

        if (items.TryGetValue(CurrentTenantService.HttpContextItemKey, out var tid) && tid is long id)
            logEvent.AddPropertyIfAbsent(factory.CreateProperty("TenantId", id));
    }
}
