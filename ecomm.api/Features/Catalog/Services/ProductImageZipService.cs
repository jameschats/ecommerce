using System.IO.Compression;
using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Media;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Catalog.Services;

public sealed record ZipImageResultDto(
    int FilesInZip,
    int Matched,
    int ProductsUpdated,
    IReadOnlyList<string> Unmatched,
    IReadOnlyList<string> Skipped,
    IReadOnlyList<string> ProductsWithoutImages);

public interface IProductImageZipService
{
    Task<ZipImageResultDto> ImportAsync(Stream zip, bool replaceExisting, CancellationToken ct = default);
}

/// <summary>
/// Bulk product images from a ZIP, matched to products by Design No (design.md §10.2).
///
/// Nobody is going to paste 400 URLs into a spreadsheet or upload 400 files one at a time.
/// Naming files after the design number is the one convention the trade already thinks in.
///
/// Accepted names, where DESIGNNO is the product SKU:
///   DESIGNNO.jpg          single image
///   DESIGNNO-1.jpg        first image  (becomes primary)
///   DESIGNNO-2.jpg        second image
/// </summary>
public sealed class ProductImageZipService : IProductImageZipService
{
    private const long Tenant = 1;
    private const int MaxEntries = 2000;
    private const long MaxEntryBytes = 8L * 1024 * 1024;

    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp"];

    private readonly EcommerceDbContext _db;
    private readonly IMediaStorage _storage;
    private readonly ILogger<ProductImageZipService> _logger;

    public ProductImageZipService(EcommerceDbContext db, IMediaStorage storage, ILogger<ProductImageZipService> logger)
    {
        _db = db;
        _storage = storage;
        _logger = logger;
    }

    public async Task<ZipImageResultDto> ImportAsync(Stream zip, bool replaceExisting, CancellationToken ct = default)
    {
        // SKU → ProductId. Loaded once; matching 400 filenames with 400 queries would be
        // needlessly slow and hammer the database for no reason.
        var products = await _db.Products
            .Where(p => p.TenantId == Tenant && !p.IsDeleted)
            .Select(p => new { p.ProductId, p.Sku, p.DesignNo })
            .ToListAsync(ct);

        // Keyed by Design No first, since that is what files are named after; SKU is added
        // only where it does not collide, so a design number always wins over a stock code
        // that happens to look the same.
        var bySku = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in products.Where(p => !string.IsNullOrWhiteSpace(p.DesignNo)))
            bySku.TryAdd(p.DesignNo!.Trim(), p.ProductId);
        foreach (var p in products.Where(p => !string.IsNullOrWhiteSpace(p.Sku)))
            bySku.TryAdd(p.Sku.Trim(), p.ProductId);

        using var archive = new ZipArchive(zip, ZipArchiveMode.Read, leaveOpen: true);

        if (archive.Entries.Count > MaxEntries)
            throw new AppException($"That ZIP has {archive.Entries.Count} entries; the limit is {MaxEntries}.");

        var unmatched = new List<string>();
        var skipped = new List<string>();
        // productId → images found for it, in filename order
        var found = new Dictionary<long, List<(int Order, string Url)>>();
        var filesConsidered = 0;

        foreach (var entry in archive.Entries)
        {
            ct.ThrowIfCancellationRequested();

            // Directory entries, and the metadata folders every Mac-made ZIP carries.
            if (string.IsNullOrEmpty(entry.Name)) continue;
            if (entry.FullName.StartsWith("__MACOSX/", StringComparison.OrdinalIgnoreCase)
                || entry.Name.StartsWith("._", StringComparison.Ordinal)
                || entry.Name.Equals(".DS_Store", StringComparison.OrdinalIgnoreCase)) continue;

            var extension = Path.GetExtension(entry.Name).ToLowerInvariant();
            if (!AllowedExtensions.Contains(extension))
            {
                skipped.Add($"{entry.Name} — not an image");
                continue;
            }

            filesConsidered++;

            if (entry.Length > MaxEntryBytes)
            {
                skipped.Add($"{entry.Name} — larger than 8 MB");
                continue;
            }

            var stem = Path.GetFileNameWithoutExtension(entry.Name);
            var match = Resolve(stem, bySku);
            if (match is null)
            {
                // Never silently discarded: an unmatched file is how you find out a design
                // number was mistyped, and a quietly incomplete catalogue is much worse.
                unmatched.Add(entry.Name);
                continue;
            }

            var (productId, order) = match.Value;

            try
            {
                await using var stream = entry.Open();
                using var buffer = new MemoryStream();
                await stream.CopyToAsync(buffer, ct);
                buffer.Position = 0;

                // watermark: true — this path is exclusively product photos.
                var stored = await _storage.SaveAsync(buffer, entry.Name, ContentTypeFor(extension), watermark: true, ct: ct);

                if (!found.TryGetValue(productId, out var list))
                    found[productId] = list = [];
                list.Add((order, stored.Url));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to store {File} from image ZIP", entry.Name);
                skipped.Add($"{entry.Name} — could not be saved");
            }
        }

        // Apply. Replacing per product rather than globally: a ZIP containing only three
        // designs must not wipe the images of the other 397.
        foreach (var (productId, images) in found)
        {
            if (replaceExisting)
            {
                var existing = await _db.ProductImages.Where(i => i.ProductId == productId).ToListAsync(ct);
                _db.ProductImages.RemoveRange(existing);
            }

            var ordered = images.OrderBy(i => i.Order).ToList();
            var alreadyHasPrimary = !replaceExisting
                && await _db.ProductImages.AnyAsync(i => i.ProductId == productId && i.IsPrimary, ct);

            for (var i = 0; i < ordered.Count; i++)
            {
                _db.ProductImages.Add(new ProductImage
                {
                    ProductId = productId,
                    Url = ordered[i].Url,
                    DisplayOrder = ordered[i].Order,
                    // Lowest-numbered image becomes primary, unless one is already set and
                    // we are adding rather than replacing.
                    IsPrimary = i == 0 && !alreadyHasPrimary,
                    CreatedAt = DateTime.UtcNow,
                });
            }
        }

        await _db.SaveChangesAsync(ct);

        var withoutImages = await _db.Products
            .Where(p => p.TenantId == Tenant && !p.IsDeleted && p.IsActive && !p.Images.Any())
            .OrderBy(p => p.Sku)
            .Select(p => p.Sku)
            .Take(50)
            .ToListAsync(ct);

        return new ZipImageResultDto(
            filesConsidered,
            found.Sum(f => f.Value.Count),
            found.Count,
            unmatched,
            skipped,
            withoutImages);
    }

    /// <summary>
    /// Resolves a filename stem to a product and an image position.
    ///
    /// Design numbers contain hyphens themselves — DESK-2026, WALL-4SHEET — so "desk-1-1"
    /// is genuinely ambiguous: SKU "desk-1" image 1, or a SKU literally called "desk-1-1"?
    ///
    /// The whole stem is tried as a SKU **first**. Only if no product has that exact SKU is
    /// a trailing "-N" treated as an image index. That ordering means a real product named
    /// DESK-1-1 always wins over a positional reading of DESK-1, so an existing design can
    /// never be quietly misfiled by a naming coincidence.
    /// </summary>
    private static (long ProductId, int Order)? Resolve(string stem, Dictionary<string, long> bySku)
    {
        if (bySku.TryGetValue(stem, out var exactId)) return (exactId, 1);

        var dash = stem.LastIndexOf('-');
        if (dash <= 0 || dash == stem.Length - 1) return null;

        var suffix = stem[(dash + 1)..];
        if (!int.TryParse(suffix, out var order) || order < 1) return null;

        var baseSku = stem[..dash];
        return bySku.TryGetValue(baseSku, out var id) ? (id, order) : null;
    }

    private static string ContentTypeFor(string extension) => extension switch
    {
        ".png" => "image/png",
        ".webp" => "image/webp",
        _ => "image/jpeg",
    };
}
