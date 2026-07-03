using System.Text.Json;
using ClosedXML.Excel;
using ecomm.api.Common;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Catalog.Dtos;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Catalog.Services;

public interface IProductImportService
{
    Task<ImportResultDto> ImportAsync(Stream xlsx, string? fileName, long? userId, CancellationToken ct = default);
    Task<byte[]> ExportAsync(CancellationToken ct = default);
    byte[] Template();
}

/// <summary>
/// Excel (.xlsx) product import/export. Known columns map to product fields;
/// any extra column is treated as a dynamic Attribute (auto-created if new).
/// Upserts by SKU. Each row is isolated — a bad row fails without aborting the rest.
/// </summary>
public sealed class ProductImportService : IProductImportService
{
    private long Tenant => _db.CurrentTenantId;

    private static readonly string[] Headers =
        ["SKU", "Name", "Category", "Brand", "Price", "CompareAtPrice", "CostPrice", "HsnCode", "Status", "ShortDescription", "ImageUrl"];

    private static readonly HashSet<string> KnownColumns = new(StringComparer.OrdinalIgnoreCase)
        { "sku", "name", "category", "brand", "price", "compareatprice", "costprice", "hsncode", "status", "shortdescription", "description", "imageurl" };

    private readonly EcommerceDbContext _db;

    public ProductImportService(EcommerceDbContext db) => _db = db;

    public byte[] Template()
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Products");
        for (var i = 0; i < Headers.Length; i++) ws.Cell(1, i + 1).Value = Headers[i];
        // Example row (replace with your products). Category must already exist; Brand is auto-created.
        ws.Cell(2, 1).Value = "SAMPLE-001";
        ws.Cell(2, 2).Value = "Sample Wall Calendar 2026";
        ws.Cell(2, 3).Value = "Wall Calendars";
        ws.Cell(2, 4).Value = "";
        ws.Cell(2, 5).Value = 499;
        ws.Cell(2, 8).Value = "4910";
        ws.Cell(2, 9).Value = "Active";
        ws.Cell(2, 11).Value = "https://example.com/your-image.jpg";
        ws.Row(1).Style.Font.Bold = true;
        return Save(wb);
    }

    public async Task<byte[]> ExportAsync(CancellationToken ct = default)
    {
        var products = await _db.Products
            .Where(p => p.TenantId == Tenant && !p.IsDeleted)
            .OrderBy(p => p.Name)
            .Select(p => new
            {
                p.Sku, p.Name, Category = p.Category!.Name, Brand = p.Brand != null ? p.Brand.Name : "",
                p.Price, p.CompareAtPrice, p.CostPrice, p.HsnCode, p.Status, p.ShortDescription,
                ImageUrl = p.Images.OrderByDescending(i => i.IsPrimary).Select(i => i.Url).FirstOrDefault(),
            })
            .ToListAsync(ct);

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Products");
        for (var i = 0; i < Headers.Length; i++) ws.Cell(1, i + 1).Value = Headers[i];
        ws.Row(1).Style.Font.Bold = true;

        var r = 2;
        foreach (var p in products)
        {
            ws.Cell(r, 1).Value = p.Sku;
            ws.Cell(r, 2).Value = p.Name;
            ws.Cell(r, 3).Value = p.Category;
            ws.Cell(r, 4).Value = p.Brand;
            ws.Cell(r, 5).Value = p.Price;
            ws.Cell(r, 6).Value = p.CompareAtPrice;
            ws.Cell(r, 7).Value = p.CostPrice;
            ws.Cell(r, 8).Value = p.HsnCode;
            ws.Cell(r, 9).Value = p.Status;
            ws.Cell(r, 10).Value = p.ShortDescription;
            ws.Cell(r, 11).Value = p.ImageUrl;
            r++;
        }
        return Save(wb);
    }

    public async Task<ImportResultDto> ImportAsync(Stream xlsx, string? fileName, long? userId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var job = new ImportJob
        {
            TenantId = Tenant, JobType = "Products", FileName = fileName, Status = "Processing",
            StartedAt = now, CreatedBy = userId, CreatedAt = now,
        };
        _db.ImportJobs.Add(job);
        await _db.SaveChangesAsync(ct);

        using var wb = new XLWorkbook(xlsx);
        var ws = wb.Worksheet(1);
        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
        var columns = MapHeaders(ws);

        var failed = new List<ImportJobItemDto>();
        int total = 0, success = 0, fail = 0;

        for (var row = 2; row <= lastRow; row++)
        {
            if (ws.Row(row).IsEmpty()) continue;
            total++;
            string Cell(string name) => columns.TryGetValue(name, out var c) ? ws.Cell(row, c).GetString().Trim() : string.Empty;

            try
            {
                await ImportRowAsync(ws, row, columns, Cell, now, ct);
                _db.ImportJobItems.Add(new ImportJobItem { ImportJobId = job.ImportJobId, RowNumber = row, Status = "Success", CreatedAt = now });
                success++;
            }
            catch (Exception ex)
            {
                fail++;
                failed.Add(new ImportJobItemDto(row, "Failed", ex.Message));
                _db.ImportJobItems.Add(new ImportJobItem
                {
                    ImportJobId = job.ImportJobId, RowNumber = row, Status = "Failed",
                    ErrorMessage = ex.Message, RawData = JsonSerializer.Serialize(new { sku = Cell("sku"), name = Cell("name") }), CreatedAt = now,
                });
            }
        }

        job.TotalRows = total;
        job.SuccessRows = success;
        job.FailedRows = fail;
        job.Status = fail == 0 ? "Completed" : success == 0 ? "Failed" : "PartiallyCompleted";
        job.CompletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return new ImportResultDto(ToDto(job), failed);
    }

    private async Task ImportRowAsync(IXLWorksheet ws, int row, Dictionary<string, int> columns, Func<string, string> cell, DateTime now, CancellationToken ct)
    {
        var sku = cell("sku");
        var name = cell("name");
        if (string.IsNullOrWhiteSpace(sku) || string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("SKU and Name are required.");

        var categoryName = cell("category");
        if (string.IsNullOrWhiteSpace(categoryName))
            throw new InvalidOperationException("Category is required.");
        var category = await _db.Categories.FirstOrDefaultAsync(c => c.TenantId == Tenant && c.Name == categoryName, ct)
            ?? throw new InvalidOperationException($"Category not found: {categoryName}");

        var priceText = cell("price");
        if (!decimal.TryParse(priceText, out var price) || price < 0)
            throw new InvalidOperationException($"Invalid price: '{priceText}'");

        long? brandId = null;
        var brandName = cell("brand");
        if (!string.IsNullOrWhiteSpace(brandName))
        {
            var brand = await _db.Brands.FirstOrDefaultAsync(b => b.TenantId == Tenant && b.Name == brandName, ct);
            if (brand is null)
            {
                brand = new Brand { TenantId = Tenant, Name = brandName, Slug = await UniqueBrandSlug(brandName, ct), IsActive = true, CreatedAt = now };
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
        if (decimal.TryParse(cell("compareatprice"), out var cmp)) product.CompareAtPrice = cmp;
        if (decimal.TryParse(cell("costprice"), out var cost)) product.CostPrice = cost;
        var hsn = cell("hsncode"); if (hsn.Length > 0) product.HsnCode = hsn;
        var status = cell("status"); product.Status = status is "Active" or "Inactive" or "Draft" ? status : "Active";
        var shortDesc = cell("shortdescription"); if (shortDesc.Length > 0) product.ShortDescription = shortDesc;

        if (isNew)
        {
            product.Slug = await UniqueProductSlug(name, ct);
            _db.Products.Add(product);
        }
        else
        {
            product.UpdatedAt = now;
        }

        var imageUrl = cell("imageurl");
        if (imageUrl.Length > 0)
        {
            _db.ProductImages.RemoveRange(product.Images);
            product.Images = new List<ProductImage> { new() { Url = imageUrl, IsPrimary = true, CreatedAt = now } };
        }

        await _db.SaveChangesAsync(ct);
        await ApplyAttributeColumns(ws, row, columns, product.ProductId, now, ct);
    }

    private async Task ApplyAttributeColumns(IXLWorksheet ws, int row, Dictionary<string, int> columns, long productId, DateTime now, CancellationToken ct)
    {
        foreach (var (header, col) in columns)
        {
            if (KnownColumns.Contains(header)) continue;
            var value = ws.Cell(row, col).GetString().Trim();
            if (value.Length == 0) continue;

            var code = Slug.From(header);
            var attribute = await _db.Attributes.FirstOrDefaultAsync(a => a.TenantId == Tenant && a.Code == code, ct);
            if (attribute is null)
            {
                attribute = new AttributeDefinition { TenantId = Tenant, Name = header, Code = code, DataType = "string", IsActive = true, CreatedAt = now };
                _db.Attributes.Add(attribute);
                await _db.SaveChangesAsync(ct);
            }

            var existing = await _db.ProductAttributeValues
                .FirstOrDefaultAsync(p => p.ProductId == productId && p.AttributeId == attribute.AttributeId, ct);
            if (existing is null)
                _db.ProductAttributeValues.Add(new ProductAttributeValue { ProductId = productId, AttributeId = attribute.AttributeId, ValueText = value, CreatedAt = now });
            else
                existing.ValueText = value;
            await _db.SaveChangesAsync(ct);
        }
    }

    private static Dictionary<string, int> MapHeaders(IXLWorksheet ws)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in ws.Row(1).CellsUsed())
        {
            var key = cell.GetString().Trim();
            if (key.Length > 0 && !map.ContainsKey(key)) map[key] = cell.Address.ColumnNumber;
        }
        return map;
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
