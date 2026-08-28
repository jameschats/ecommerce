using System.Collections.Concurrent;
using ecomm.api.Common.Tenancy;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Commerce;

/// <summary>
/// In-memory buffer for storefront behavioural events (AI Commerce data layer). Singleton — shared
/// between the request-scoped ingest path and the Hangfire flush job. Deliberately fire-and-forget:
/// analytics-grade data, so a handful of events lost on a restart is acceptable, and keeping them off
/// the storefront's synchronous write path matters far more than perfect durability. Bounded so a burst
/// (or a stalled flush) can never grow memory without limit.
/// </summary>
public sealed class CustomerEventBuffer
{
    private const int MaxBuffered = 50_000;
    private readonly ConcurrentQueue<CustomerEvent> _queue = new();
    private int _count;

    public void Add(CustomerEvent e)
    {
        if (Volatile.Read(ref _count) >= MaxBuffered) return;   // shed load rather than OOM
        _queue.Enqueue(e);
        Interlocked.Increment(ref _count);
    }

    public List<CustomerEvent> DrainUpTo(int max)
    {
        var outp = new List<CustomerEvent>(Math.Min(max, 4096));
        while (outp.Count < max && _queue.TryDequeue(out var e))
        {
            outp.Add(e);
            Interlocked.Decrement(ref _count);
        }
        return outp;
    }
}

public interface ICustomerEventFlushService
{
    /// <summary>Hangfire recurring job: drain the buffer and batch-insert, per tenant, respecting each store's tracking toggle.</summary>
    Task FlushAsync(CancellationToken ct = default);
}

/// <summary>
/// Drains <see cref="CustomerEventBuffer"/> and writes events in batches. Runs outside a request, so each
/// tenant's rows are inserted under that tenant's scope (<see cref="ICurrentTenantService.BeginScope"/>) —
/// the ITenantScoped auto-stamp then sets the correct TenantId, and one store's events can't be written
/// under another's. Stores with behaviour tracking turned off have their buffered events dropped here.
/// </summary>
public sealed class CustomerEventFlushService(
    EcommerceDbContext db, CustomerEventBuffer buffer, ICurrentTenantService tenant, ILogger<CustomerEventFlushService> log)
    : ICustomerEventFlushService
{
    private const int BatchCap = 5000;

    public async Task FlushAsync(CancellationToken ct = default)
    {
        var events = buffer.DrainUpTo(BatchCap);
        if (events.Count == 0) return;

        // Which tenants in this batch have tracking switched OFF (default is ON).
        var tenantIds = events.Select(e => e.TenantId).Distinct().ToList();
        var disabled = await db.Settings.IgnoreQueryFilters()
            .Where(s => s.SettingKey == "BehaviorTrackingEnabled" && s.SettingValue == "false" && tenantIds.Contains(s.TenantId))
            .Select(s => s.TenantId).ToHashSetAsync(ct);

        var written = 0;
        foreach (var group in events.GroupBy(e => e.TenantId))
        {
            if (disabled.Contains(group.Key)) continue;
            using (tenant.BeginScope(group.Key))
            {
                db.CustomerEvents.AddRange(group);
                await db.SaveChangesAsync(ct);
                db.ChangeTracker.Clear();   // keep the shared context light across groups
                written += group.Count();
            }
        }
        if (written > 0) log.LogDebug("Flushed {Written} customer events across {Tenants} tenant(s).", written, tenantIds.Count);
    }
}
