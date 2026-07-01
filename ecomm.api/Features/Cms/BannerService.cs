using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Cms;

/// <summary>What the storefront renders per banner (image already resolved to a URL).</summary>
public sealed record BannerDto(long HomeBannerId, string? ImageUrl, string? Title, string? Subtitle, string? Cta, string? Link);

/// <summary>Full banner for the admin editor.</summary>
public sealed record AdminBannerDto(
    long HomeBannerId, string? Title, string? Subtitle, string? CtaText, string? LinkUrl,
    string? ImageUrl, bool HasUpload, int DisplayOrder, bool IsActive);

public sealed record BannerUpsert(
    string? Title, string? Subtitle, string? CtaText, string? LinkUrl, string? ImageUrl, int DisplayOrder, bool IsActive);

public interface IBannerService
{
    Task<List<BannerDto>> GetActiveAsync(CancellationToken ct = default);
    Task<List<AdminBannerDto>> GetAllAsync(CancellationToken ct = default);
    Task<AdminBannerDto> CreateAsync(BannerUpsert req, CancellationToken ct = default);
    Task<AdminBannerDto> UpdateAsync(long id, BannerUpsert req, CancellationToken ct = default);
    Task DeleteAsync(long id, CancellationToken ct = default);
    Task SetImageAsync(long id, byte[] data, string contentType, CancellationToken ct = default);
    Task<(byte[] Data, string ContentType)?> GetImageAsync(long id, CancellationToken ct = default);
}

public sealed class BannerService : IBannerService
{
    private const long Tenant = 1;
    private readonly EcommerceDbContext _db;

    public BannerService(EcommerceDbContext db) => _db = db;

    public async Task<List<BannerDto>> GetActiveAsync(CancellationToken ct = default)
    {
        var rows = await _db.HomeBanners.AsNoTracking()
            .Where(b => b.TenantId == Tenant && b.IsActive)
            .OrderBy(b => b.DisplayOrder).ThenBy(b => b.HomeBannerId)
            .Select(b => new { b.HomeBannerId, b.Title, b.Subtitle, b.CtaText, b.LinkUrl, b.ImageUrl, HasUpload = b.ImageData != null, b.UpdatedAt, b.CreatedAt })
            .ToListAsync(ct);

        return rows.Select(b => new BannerDto(
            b.HomeBannerId, ResolveImage(b.HomeBannerId, b.HasUpload, b.ImageUrl, b.UpdatedAt ?? b.CreatedAt),
            b.Title, b.Subtitle, b.CtaText, b.LinkUrl)).ToList();
    }

    public async Task<List<AdminBannerDto>> GetAllAsync(CancellationToken ct = default)
    {
        var rows = await _db.HomeBanners.AsNoTracking()
            .Where(b => b.TenantId == Tenant)
            .OrderBy(b => b.DisplayOrder).ThenBy(b => b.HomeBannerId)
            .Select(b => new { b.HomeBannerId, b.Title, b.Subtitle, b.CtaText, b.LinkUrl, b.ImageUrl, HasUpload = b.ImageData != null, b.DisplayOrder, b.IsActive, b.UpdatedAt, b.CreatedAt })
            .ToListAsync(ct);

        return rows.Select(b => new AdminBannerDto(
            b.HomeBannerId, b.Title, b.Subtitle, b.CtaText, b.LinkUrl,
            ResolveImage(b.HomeBannerId, b.HasUpload, b.ImageUrl, b.UpdatedAt ?? b.CreatedAt),
            b.HasUpload, b.DisplayOrder, b.IsActive)).ToList();
    }

    public async Task<AdminBannerDto> CreateAsync(BannerUpsert req, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var b = new HomeBanner
        {
            TenantId = Tenant,
            Title = req.Title, Subtitle = req.Subtitle, CtaText = req.CtaText, LinkUrl = req.LinkUrl,
            ImageUrl = string.IsNullOrWhiteSpace(req.ImageUrl) ? null : req.ImageUrl.Trim(),
            DisplayOrder = req.DisplayOrder, IsActive = req.IsActive, CreatedAt = now,
        };
        _db.HomeBanners.Add(b);
        await _db.SaveChangesAsync(ct);
        return ToAdmin(b);
    }

    public async Task<AdminBannerDto> UpdateAsync(long id, BannerUpsert req, CancellationToken ct = default)
    {
        var b = await Find(id, ct);
        b.Title = req.Title; b.Subtitle = req.Subtitle; b.CtaText = req.CtaText; b.LinkUrl = req.LinkUrl;
        b.ImageUrl = string.IsNullOrWhiteSpace(req.ImageUrl) ? null : req.ImageUrl.Trim();
        b.DisplayOrder = req.DisplayOrder; b.IsActive = req.IsActive; b.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ToAdmin(b);
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        var b = await Find(id, ct);
        _db.HomeBanners.Remove(b);
        await _db.SaveChangesAsync(ct);
    }

    public async Task SetImageAsync(long id, byte[] data, string contentType, CancellationToken ct = default)
    {
        var b = await Find(id, ct);
        b.ImageData = data;
        b.ImageContentType = contentType;
        b.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<(byte[] Data, string ContentType)?> GetImageAsync(long id, CancellationToken ct = default)
    {
        var b = await _db.HomeBanners.AsNoTracking()
            .Where(x => x.TenantId == Tenant && x.HomeBannerId == id && x.ImageData != null)
            .Select(x => new { x.ImageData, x.ImageContentType })
            .FirstOrDefaultAsync(ct);
        return b?.ImageData is null ? null : (b.ImageData, b.ImageContentType ?? "image/jpeg");
    }

    private async Task<HomeBanner> Find(long id, CancellationToken ct) =>
        await _db.HomeBanners.FirstOrDefaultAsync(x => x.TenantId == Tenant && x.HomeBannerId == id, ct)
        ?? throw new AppException("Banner not found.", 404);

    // Uploaded image → API URL (cache-busted by last-modified ticks); else the external URL.
    private static string? ResolveImage(long id, bool hasUpload, string? imageUrl, DateTime stamp) =>
        hasUpload ? $"/api/cms/banners/{id}/image?v={stamp.Ticks}" : imageUrl;

    private static AdminBannerDto ToAdmin(HomeBanner b) => new(
        b.HomeBannerId, b.Title, b.Subtitle, b.CtaText, b.LinkUrl,
        ResolveImage(b.HomeBannerId, b.ImageData != null, b.ImageUrl, b.UpdatedAt ?? b.CreatedAt),
        b.ImageData != null, b.DisplayOrder, b.IsActive);
}
