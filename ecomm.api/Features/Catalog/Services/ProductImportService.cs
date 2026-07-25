using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using ecomm.api.Common;
using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Catalog.Dtos;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Catalog.Services;

public interface IProductImportService
{
    Task<ImportPreviewDto> PreviewAsync(Stream file, string? fileName, CancellationToken ct = default);
    Task<ImportResultDto> ImportAsync(Stream file, string? fileName, long? userId, CancellationToken ct = default);
    Task<byte[]> ExportAsync(CancellationToken ct = default);
    byte[] Template();
}

/// <summary>
/// Catalogue import from .xlsx or .csv (design.md §10.1, §23).
///
/// Upserts by SKU. Any column that is not a known field becomes a product attribute,
/// auto-created if new — that is how <c>Content</c>, paper GSM and anything else the
/// business invents later arrive without a code change.
///
/// Every row is isolated: a bad row fails alone and is reported by row number, rather than
/// aborting a 400-row upload over one typo.
/// </summary>
public sealed class ProductImportService : IProductImportService
{
    private const long Tenant = 1;

    private static readonly string[] Headers =
    [
        "SKU", "DesignNo", "Name", "Category", "Brand", "Price", "MRP", "CostPrice",
        "HsnCode", "Status", "SortOrder", "ShortDescription", "ImageUrl", "Content",
    ];

    /// <summary>
    /// Columns the importer maps to product fields. Anything else becomes an attribute —
    /// which is why <c>Content</c> is absent here despite being in the template.
    /// </summary>
    private static readonly HashSet<string> KnownColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "sku", "designno", "design no", "name", "category", "brand", "price", "mrp",
        "compareatprice", "costprice", "cost", "hsncode", "status", "sortorder",
        "shortdescription", "description", "imageurl",
    };

    private readonly EcommerceDbContext _db;

    public ProductImportService(EcommerceDbContext db) => _db = db;

    // ------------------------------------------------------------------ template / export

    public byte[] Template()
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Products");
        for (var i = 0; i < Headers.Length; i++) ws.Cell(1, i + 1).Value = Headers[i];

        ws.Cell(2, 1).Value = "CAL-1042";
        ws.Cell(2, 2).Value = "D-1042";
        ws.Cell(2, 3).Value = "10 x 15 Art Mount Lamination";
        ws.Cell(2, 4).Value = "Calendar Mount - 10\" x 15\" Lamination Art";
        ws.Cell(2, 6).Value = 4.1;
        ws.Cell(2, 7).Value = 6;
        ws.Cell(2, 8).Value = 2;
        ws.Cell(2, 9).Value = "4910";
        ws.Cell(2, 10).Value = "Active";
        ws.Cell(2, 11).Value = 10;
        ws.Cell(2, 14).Value = "1 Box (50 Pcs)";

        ws.Row(1).Style.Font.Bold = true;
        ws.Columns().AdjustToContents();
        return Save(wb);
    }

    public async Task<byte[]> ExportAsync(CancellationToken ct = default)
    {
        var products = await _db.Products
            .Where(p => p.TenantId == Tenant && !p.IsDeleted)
            .OrderBy(p => p.Category!.DisplayOrder).ThenBy(p => p.SortOrder).ThenBy(p => p.Name)
            .Select(p => new
            {
                p.Sku, p.DesignNo, p.Name, Category = p.Category!.Name,
                Brand = p.Brand != null ? p.Brand.Name : "",
                p.Price, p.CompareAtPrice, p.CostPrice, p.HsnCode, p.Status, p.SortOrder, p.ShortDescription,
                ImageUrl = p.Images.OrderByDescending(i => i.IsPrimary).Select(i => i.Url).FirstOrDefault(),
                Content = p.AttributeValues.Where(a => a.Attribute!.Name == "Content")
                    .Select(a => a.ValueText).FirstOrDefault(),
            })
            .ToListAsync(ct);

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Products");
        for (var i = 0; i < Headers.Length; i++) ws.Cell(1, i + 1).Value = Headers[i];

        var r = 2;
        foreach (var p in products)
        {
            ws.Cell(r, 1).Value = p.Sku;
            ws.Cell(r, 2).Value = p.DesignNo;
            ws.Cell(r, 3).Value = p.Name;
            ws.Cell(r, 4).Value = p.Category;
            ws.Cell(r, 5).Value = p.Brand;
            ws.Cell(r, 6).Value = p.Price;
            ws.Cell(r, 7).Value = p.CompareAtPrice;
            ws.Cell(r, 8).Value = p.CostPrice;
            ws.Cell(r, 9).Value = p.HsnCode;
            ws.Cell(r, 10).Value = p.Status;
            ws.Cell(r, 11).Value = p.SortOrder;
            ws.Cell(r, 12).Value = p.ShortDescription;
            ws.Cell(r, 13).Value = p.ImageUrl;
            ws.Cell(r, 14).Value = p.Content;
            r++;
        }
        ws.Row(1).Style.Font.Bold = true;
        return Save(wb);
    }

    // ------------------------------------------------------------------ preview

    /// <summary>
    /// Parses and validates without writing anything (design.md §10.1).
    ///
    /// With 400 rows in one file, an un-previewed import is a genuinely dangerous button —
    /// this is what lets an operator see "112 new, 289 updated, 3 errors" and cancel.
    /// </summary>
    public async Task<ImportPreviewDto> PreviewAsync(Stream file, string? fileName, CancellationToken ct = default)
    {
        var sheet = SheetReader.Read(file, fileName);
        if (sheet.Rows.Count == 0) throw new AppException("That file has no data rows.");

        var existingSkus = await _db.Products
            .Where(p => p.TenantId == Tenant && !p.IsDeleted)
            .Select(p => p.Sku)
            .ToListAsync(ct);
        var known = new HashSet<string>(existingSkus, StringComparer.OrdinalIgnoreCase);

        var existingCategories = await _db.Categories
            .Where(c => c.TenantId == Tenant)
            .Select(c => c.Name)
            .ToListAsync(ct);
        var categories = new HashSet<string>(existingCategories, StringComparer.OrdinalIgnoreCase);

        var errors = new List<ImportJobItemDto>();
        var newCategories = new List<string>();
        var seenSkus = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var seenDesigns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var duplicates = new List<string>();
        int create = 0, update = 0;

        foreach (var row in sheet.Rows)
        {
            var sku = row.Get("SKU");
            var name = row.Get("Name");

            if (sku.Length == 0 || name.Length == 0)
            {
                errors.Add(new ImportJobItemDto(row.RowNumber, "Failed", "SKU and Name are required."));
                continue;
            }

            // Duplicates within the file matter as much as clashes with the database —
            // two rows sharing a SKU means the second silently overwrites the first.
            if (seenSkus.TryGetValue(sku, out var firstRow))
                duplicates.Add($"Row {row.RowNumber}: SKU '{sku}' already used on row {firstRow}.");
            else seenSkus[sku] = row.RowNumber;

            var designNo = FirstNonEmpty(row, "DesignNo", "Design No");
            if (designNo.Length > 0)
            {
                if (seenDesigns.TryGetValue(designNo, out var firstDesignRow))
                    duplicates.Add($"Row {row.RowNumber}: Design No '{designNo}' already used on row {firstDesignRow}.");
                else seenDesigns[designNo] = row.RowNumber;
            }

            var price = FirstNonEmpty(row, "Price");
            if (!decimal.TryParse(price, out var parsed) || parsed < 0)
            {
                errors.Add(new ImportJobItemDto(row.RowNumber, "Failed", $"Invalid price '{price}'."));
                continue;
            }

            var category = row.Get("Category");
            if (category.Length == 0)
            {
                errors.Add(new ImportJobItemDto(row.RowNumber, "Failed", "Category is required."));
                continue;
            }
            if (!categories.Contains(category))
            {
                categories.Add(category);
                newCategories.Add(category);
            }

            if (known.Contains(sku)) update++; else create++;
        }

        var attributeColumns = sheet.Headers
            .Where(h => !KnownColumns.Contains(h) && !h.Equals("Content", StringComparison.OrdinalIgnoreCase))
            .ToList();

        return new ImportPreviewDto(
            sheet.Rows.Count, create, update, errors.Count,
            errors.Take(50).ToList(), newCategories, duplicates.Take(50).ToList(), attributeColumns);
    }

    // ------------------------------------------------------------------ import

    public async Task<ImportResultDto> ImportAsync(Stream file, string? fileName, long? userId, CancellationToken ct = default)
    {
        var sheet = SheetReader.Read(file, fileName);
        if (sheet.Rows.Count == 0) throw new AppException("That file has no data rows.");

        var now = DateTime.UtcNow;
        var job = new ImportJob
        {
            TenantId = Tenant, JobType = "Products", FileName = fileName, Status = "Processing",
            StartedAt = now, CreatedBy = userId, CreatedAt = now,
        };
        _db.ImportJobs.Add(job);
        await _db.SaveChangesAsync(ct);

        var failed = new List<ImportJobItemDto>();
        int success = 0, fail = 0;

        foreach (var row in sheet.Rows)
        {
            try
            {
                await ImportRowAsync(row, sheet.Headers, now, ct);
                _db.ImportJobItems.Add(new ImportJobItem
                {
                    ImportJobId = job.ImportJobId, RowNumber = row.RowNumber, Status = "Success", CreatedAt = now,
                });
                success++;
            }
            catch (Exception ex)
            {
                fail++;
                failed.Add(new ImportJobItemDto(row.RowNumber, "Failed", ex.Message));
                _db.ImportJobItems.Add(new ImportJobItem
                {
                    ImportJobId = job.ImportJobId, RowNumber = row.RowNumber, Status = "Failed",
                    ErrorMessage = ex.Message,
                    RawData = JsonSerializer.Serialize(new { sku = row.Get("SKU"), name = row.Get("Name") }),
                    CreatedAt = now,
                });
            }
        }

        job.TotalRows = sheet.Rows.Count;
        job.SuccessRows = success;
        job.FailedRows = fail;
        job.Status = fail == 0 ? "Completed" : success == 0 ? "Failed" : "PartiallyCompleted";
        job.CompletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return new ImportResultDto(ToDto(job), failed);
    }

    private async Task ImportRowAsync(SheetRow row, IReadOnlyList<string> headers, DateTime now, CancellationToken ct)
    {
        var sku = row.Get("SKU");
        var name = row.Get("Name");
        if (sku.Length == 0 || name.Length == 0)
            throw new InvalidOperationException("SKU and Name are required.");

        var categoryName = row.Get("Category");
        if (categoryName.Length == 0) throw new InvalidOperationException("Category is required.");
        var category = await GetOrCreateCategoryAsync(categoryName, now, ct);

        var priceText = FirstNonEmpty(row, "Price");
        if (!decimal.TryParse(priceText, out var price) || price < 0)
            throw new InvalidOperationException($"Invalid price: '{priceText}'");

        long? brandId = null;
        var brandName = row.Get("Brand");
        if (brandName.Length > 0)
        {
            var brand = await _db.Brands.FirstOrDefaultAsync(b => b.TenantId == Tenant && b.Name == brandName, ct);
            if (brand is null)
            {
                brand = new Brand
                {
                    TenantId = Tenant, Name = brandName, Slug = await UniqueBrandSlug(brandName, ct),
                    IsActive = true, CreatedAt = now,
                };
                _db.Brands.Add(brand);
                await _db.SaveChangesAsync(ct);
            }
            brandId = brand.BrandId;
        }

        var product = await _db.Products.Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.TenantId == Tenant && p.Sku == sku, ct);
        var isNew = product is null;
        product ??= new Product { TenantId = Tenant, Sku = sku, CreatedAt = now, IsActive = true };

        product.Name = name;
        product.CategoryId = category.CategoryId;
        product.BrandId = brandId;
        product.Price = price;

        var designNo = FirstNonEmpty(row, "DesignNo", "Design No");
        if (designNo.Length > 0) product.DesignNo = designNo;

        // "MRP" is the sheet's wording; CompareAtPrice is the column it feeds.
        var mrpText = FirstNonEmpty(row, "MRP", "CompareAtPrice");
        if (decimal.TryParse(mrpText, out var mrp)) product.CompareAtPrice = mrp;

        if (decimal.TryParse(FirstNonEmpty(row, "CostPrice", "Cost"), out var cost)) product.CostPrice = cost;
        if (int.TryParse(row.Get("SortOrder"), out var sortOrder)) product.SortOrder = sortOrder;

        var hsn = row.Get("HsnCode"); if (hsn.Length > 0) product.HsnCode = hsn;
        var status = row.Get("Status");
        product.Status = status is "Active" or "Inactive" or "Draft" ? status : "Active";
        product.IsActive = product.Status == "Active";
        var shortDesc = row.Get("ShortDescription"); if (shortDesc.Length > 0) product.ShortDescription = shortDesc;

        if (isNew)
        {
            product.Slug = await UniqueProductSlug(name, ct);
            _db.Products.Add(product);
        }
        else
        {
            product.UpdatedAt = now;
        }

        var imageUrl = row.Get("ImageUrl");
        if (imageUrl.Length > 0)
        {
            _db.ProductImages.RemoveRange(product.Images);
            product.Images = new List<ProductImage> { new() { Url = imageUrl, IsPrimary = true, CreatedAt = now } };
        }

        await _db.SaveChangesAsync(ct);
        await ApplyAttributeColumnsAsync(row, headers, product.ProductId, now, ct);
    }

    /// <summary>
    /// Finds a category by name, creating it if new (design.md §10.1). Without this a
    /// 400-row upload fails on the first row carrying a category nobody has typed in yet.
    /// </summary>
    private async Task<Category> GetOrCreateCategoryAsync(string name, DateTime now, CancellationToken ct)
    {
        var category = await _db.Categories.FirstOrDefaultAsync(c => c.TenantId == Tenant && c.Name == name, ct);
        if (category is not null) return category;

        var baseSlug = Slug.From(name);
        var slug = baseSlug;
        var n = 1;
        while (await _db.Categories.AnyAsync(c => c.TenantId == Tenant && c.Slug == slug, ct)) slug = $"{baseSlug}-{++n}";

        var maxOrder = await _db.Categories.Where(c => c.TenantId == Tenant)
            .Select(c => (int?)c.DisplayOrder).MaxAsync(ct) ?? 0;

        category = new Category
        {
            TenantId = Tenant, Name = name, Slug = slug,
            DisplayOrder = maxOrder + 10, IsActive = true, CreatedAt = now,
        };
        _db.Categories.Add(category);
        await _db.SaveChangesAsync(ct);
        return category;
    }

    private async Task ApplyAttributeColumnsAsync(
        SheetRow row, IReadOnlyList<string> headers, long productId, DateTime now, CancellationToken ct)
    {
        foreach (var header in headers)
        {
            if (KnownColumns.Contains(header)) continue;
            var value = row.Get(header);
            if (value.Length == 0) continue;

            var code = Slug.From(header);
            var attribute = await _db.Attributes.FirstOrDefaultAsync(a => a.TenantId == Tenant && a.Code == code, ct);
            if (attribute is null)
            {
                attribute = new AttributeDefinition
                {
                    TenantId = Tenant, Name = header, Code = code, DataType = "string",
                    IsActive = true, CreatedAt = now,
                };
                _db.Attributes.Add(attribute);
                await _db.SaveChangesAsync(ct);
            }

            var existing = await _db.ProductAttributeValues
                .FirstOrDefaultAsync(p => p.ProductId == productId && p.AttributeId == attribute.AttributeId, ct);
            if (existing is null)
            {
                _db.ProductAttributeValues.Add(new ProductAttributeValue
                {
                    ProductId = productId, AttributeId = attribute.AttributeId, ValueText = value, CreatedAt = now,
                });
            }
            else
            {
                existing.ValueText = value;
            }
            await _db.SaveChangesAsync(ct);
        }
    }

    /// <summary>First non-empty value among several accepted spellings of a column.</summary>
    private static string FirstNonEmpty(SheetRow row, params string[] headers)
    {
        foreach (var h in headers)
        {
            var v = row.Get(h);
            if (v.Length > 0) return v;
        }
        return string.Empty;
    }

    private async Task<string> UniqueProductSlug(string source, CancellationToken ct)
    {
        var baseSlug = Slug.From(source); var slug = baseSlug; var n = 1;
        while (await _db.Products.AnyAsync(p => p.TenantId == Tenant && p.Slug == slug, ct)) slug = $"{baseSlug}-{++n}";
        return slug;
    }

    private async Task<string> UniqueBrandSlug(string source, CancellationToken ct)
    {
        var baseSlug = Slug.From(source); var slug = baseSlug; var n = 1;
        while (await _db.Brands.AnyAsync(b => b.TenantId == Tenant && b.Slug == slug, ct)) slug = $"{baseSlug}-{++n}";
        return slug;
    }

    private static byte[] Save(XLWorkbook wb)
    {
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static ImportJobDto ToDto(ImportJob j) =>
        new(j.ImportJobId, j.JobType, j.FileName, j.Status, j.TotalRows, j.SuccessRows, j.FailedRows, j.CreatedAt, j.CompletedAt);
}
