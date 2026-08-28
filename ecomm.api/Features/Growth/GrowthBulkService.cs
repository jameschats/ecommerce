using ecomm.api.Common.Exceptions;
using ecomm.api.Common.Tenancy;
using ecomm.api.Data.Context;
using Hangfire;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Growth;

public sealed record BulkGenerateRequest(string ContentType, IReadOnlyList<long> ProductIds, string? Language, string? Brief);
public sealed record BulkJobDto(int Queued, string Message);

public interface IGrowthBulkService
{
    /// <summary>Queue one generation of <c>ContentType</c> for each selected product; runs async on Hangfire.</summary>
    Task<BulkJobDto> QueueAsync(BulkGenerateRequest req, long? userId, CancellationToken ct = default);
    /// <summary>Hangfire entry point — generates one piece under the captured tenant scope.</summary>
    Task RunOneAsync(string contentType, long productId, string? language, string? brief, long? userId, long tenantId, CancellationToken ct = default);
}

/// <summary>
/// AI Growth M1 — bulk generation. A merchant picks up to <see cref="MaxProducts"/> products and one
/// content type; each product is generated on its own background job so a large batch neither blocks the
/// request nor loses the whole run if one product fails. Each job meters credits exactly as a single
/// generation would (debit on success only); an out-of-credits job is logged and dropped, not retried.
/// </summary>
public sealed class GrowthBulkService(
    EcommerceDbContext db, IGrowthGenerationService gen, ICurrentTenantService tenant,
    IBackgroundJobClient jobs, ILogger<GrowthBulkService> log) : IGrowthBulkService
{
    private const int MaxProducts = 50;

    public async Task<BulkJobDto> QueueAsync(BulkGenerateRequest req, long? userId, CancellationToken ct = default)
    {
        var type = (req.ContentType ?? "").Trim();
        if (!gen.Types().Any(t => t.Key.Equals(type, StringComparison.OrdinalIgnoreCase)))
            throw new AppException("Unknown content type.", StatusCodes.Status400BadRequest);

        var ids = (req.ProductIds ?? Array.Empty<long>()).Distinct().ToList();
        if (ids.Count == 0) throw new AppException("Pick at least one product.", StatusCodes.Status400BadRequest);
        if (ids.Count > MaxProducts) throw new AppException($"Pick at most {MaxProducts} products per batch.", StatusCodes.Status400BadRequest);

        // Only queue products this tenant actually owns (global query filter scopes the lookup).
        var owned = await db.Products.AsNoTracking().Where(p => ids.Contains(p.ProductId)).Select(p => p.ProductId).ToListAsync(ct);
        if (owned.Count == 0) throw new AppException("None of those products were found.", StatusCodes.Status404NotFound);

        var tenantId = tenant.CurrentTenantId;
        foreach (var pid in owned)
            jobs.Enqueue<IGrowthBulkService>(s => s.RunOneAsync(type, pid, req.Language, req.Brief, userId, tenantId, CancellationToken.None));

        return new BulkJobDto(owned.Count, $"Generating {type} for {owned.Count} product(s) in the background. They'll appear in your content library shortly.");
    }

    public async Task RunOneAsync(string contentType, long productId, string? language, string? brief, long? userId, long tenantId, CancellationToken ct = default)
    {
        using (tenant.BeginScope(tenantId))
        {
            try
            {
                await gen.GenerateAsync(new GenerateRequest(contentType, productId, language, brief), userId, null, ct);
            }
            catch (AppException ex)
            {
                // Out of credits / product gone / etc. — expected, don't let Hangfire retry-storm on it.
                log.LogWarning("Bulk generate {Type} for product {Product} (tenant {Tenant}) skipped: {Message}", contentType, productId, tenantId, ex.Message);
            }
        }
    }
}
