using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using ecomm.api.Common;
using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Catalog.Dtos;
using ecomm.api.Features.Inventory;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Catalog.Services;

public interface IProductImportService
{
    Task<ImportPreviewDto> PreviewAsync(Stream file, string? fileName, CancellationToken ct = default);
    Task<ImportResultDto> ImportAsync(Stream file, string? fileName, long? userId, CancellationToken ct = default);
    Task<byte[]> ExportAsync(CancellationToken ct = default);
    byte[] Template();

    // --- variant sheet: one row per variant, a separate format/import from the above ---
    byte[] VariantSheetTemplate();
    Task<byte[]> VariantSheetExportAsync(CancellationToken ct = default);
    Task<ImportResultDto> ImportVariantSheetAsync(Stream file, string? fileName, long? userId, CancellationToken ct = default);
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
        "HsnCode", "Status", "SortOrder", "ShortDescription", "Description",
        "MetaTitle", "MetaDescription", "MetaKeywords", "Featured", "ImageUrl", "Content",
        "Option1 Name", "Option1 Values", "Option2 Name", "Option2 Values", "Option3 Name", "Option3 Values",
    ];

    /// <summary>
    /// Columns the importer maps to product fields. Anything else becomes an attribute —
    /// which is why <c>Content</c> is absent here despite being in the template.
    /// </summary>
    private static readonly HashSet<string> KnownColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "sku", "designno", "design no", "name", "category", "brand", "price", "mrp",
        "compareatprice", "costprice", "cost", "hsncode", "status", "sortorder",
        "shortdescription", "description", "metatitle", "metadescription", "metakeywords",
        "keywords", "featured", "imageurl",
        "option1 name", "option1 values", "option2 name", "option2 values", "option3 name", "option3 values",
    };

    private static readonly string[] VariantSheetHeaders =
    [
        "RowType", "SKU", "Name", "Category", "Brand", "Price", "MRP", "CostPrice",
        "HsnCode", "Status", "Featured", "ImageUrl",
        "Option1 Name", "Option2 Name", "Option3 Name",
        "Variant SKU", "Price Diff", "Active", "Stock",
        "Option1 Value", "Option2 Value", "Option3 Value",
    ];

    private readonly EcommerceDbContext _db;
    private readonly IVariantService _variants;
    private readonly IInventoryService _inventory;

    public ProductImportService(EcommerceDbContext db, IVariantService variants, IInventoryService inventory)
    {
        _db = db;
        _variants = variants;
        _inventory = inventory;
    }

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
        ws.Cell(2, 14).Value = "10x15 Art Mount Lamination — CalendarShop";
        ws.Cell(2, 15).Value = "One or two sentences quoted in search results.";
        ws.Cell(2, 16).Value = "wall calendar, wholesale, 2027";
        ws.Cell(2, 17).Value = "FALSE";
        ws.Cell(2, 19).Value = "1 Box (50 Pcs)";
        ws.Cell(2, 20).Value = "Quantity";
        ws.Cell(2, 21).Value = "100, 200, 500";
        ws.Cell(2, 22).Value = "Colour";
        ws.Cell(2, 23).Value = "Red, Blue";

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
                p.ProductId, p.Sku, p.DesignNo, p.Name, Category = p.Category!.Name,
                Brand = p.Brand != null ? p.Brand.Name : "",
                p.Price, p.CompareAtPrice, p.CostPrice, p.HsnCode, p.Status, p.SortOrder, p.ShortDescription,
                p.Description, p.MetaTitle, p.MetaDescription, p.MetaKeywords, p.IsFeatured,
                ImageUrl = p.Images.OrderByDescending(i => i.IsPrimary).Select(i => i.Url).FirstOrDefault(),
                Content = p.AttributeValues.Where(a => a.Attribute!.Name == "Content")
                    .Select(a => a.ValueText).FirstOrDefault(),
            })
            .ToListAsync(ct);

        // Each product's current option structure (Quantity: 100, 200 / Colour: Red, Blue),
        // derived from its variants — not the variants themselves. A product's individual
        // variant SKUs/price differences/stock stay UI-only (Product edit + Inventory);
        // re-exporting those as flat columns here would need a row per variant, which would
        // turn this from "one row per product" into something else entirely. This round-trips
        // the *shape* an admin defines with the option generator, which is what bulk-editing
        // many products' options at once actually needs.
        var productIds = products.Select(p => p.ProductId).ToList();
        var optionGroupsByProduct = (await _db.VariantOptions
                .Where(o => productIds.Contains(o.Variant!.ProductId))
                .Select(o => new { o.Variant!.ProductId, o.OptionName, o.OptionValue })
                .ToListAsync(ct))
            .GroupBy(o => o.ProductId)
            .ToDictionary(
                g => g.Key,
                g => g.GroupBy(o => o.OptionName, StringComparer.OrdinalIgnoreCase)
                    .Select(og => (Name: og.Key, Values: og.Select(o => o.OptionValue).Distinct().ToList()))
                    .ToList());

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
            ws.Cell(r, 13).Value = p.Description;
            ws.Cell(r, 14).Value = p.MetaTitle;
            ws.Cell(r, 15).Value = p.MetaDescription;
            ws.Cell(r, 16).Value = p.MetaKeywords;
            ws.Cell(r, 17).Value = p.IsFeatured;
            ws.Cell(r, 18).Value = p.ImageUrl;
            ws.Cell(r, 19).Value = p.Content;

            if (optionGroupsByProduct.TryGetValue(p.ProductId, out var groups))
            {
                for (var g = 0; g < groups.Count && g < 3; g++)
                {
                    ws.Cell(r, 20 + g * 2).Value = groups[g].Name;
                    ws.Cell(r, 21 + g * 2).Value = string.Join(", ", groups[g].Values);
                }
            }
            r++;
        }
        ws.Row(1).Style.Font.Bold = true;
        return Save(wb);
    }

    // ------------------------------------------------------------------ variant sheet (one row per variant)
    //
    // A second, separate template from the one above — that one stays one-row-per-product and
    // only defines variant *structure* (Option1-3 Name/Values → the generator), never touching
    // an individual variant's own SKU/price/stock. This one is for setting exactly that: each
    // product still gets one "Product" row (create/update the product, declare what its option
    // slots mean), followed by one "Variant" row per combination carrying that variant's own
    // SKU, price difference, active flag and stock — loosely modelled on how Wix's product
    // export groups a product row with its variant rows, not copied column-for-column.

    public byte[] VariantSheetTemplate()
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Products & Variants");
        for (var i = 0; i < VariantSheetHeaders.Length; i++) ws.Cell(1, i + 1).Value = VariantSheetHeaders[i];

        ws.Cell(2, 1).Value = "Product";
        ws.Cell(2, 2).Value = "CAL-2001";
        ws.Cell(2, 3).Value = "15x20 Multicolour Finished Wall Calendar";
        ws.Cell(2, 4).Value = "Finished Calendar";
        ws.Cell(2, 6).Value = 28.2;
        ws.Cell(2, 9).Value = "4910";
        ws.Cell(2, 10).Value = "Active";
        ws.Cell(2, 11).Value = "FALSE";
        ws.Cell(2, 13).Value = "Calendar Type";
        ws.Cell(2, 14).Value = "Sheeter";

        ws.Cell(3, 1).Value = "Variant";
        ws.Cell(3, 2).Value = "CAL-2001";
        ws.Cell(3, 16).Value = "CAL-2001-A";
        ws.Cell(3, 17).Value = 0.4;
        ws.Cell(3, 18).Value = "TRUE";
        ws.Cell(3, 19).Value = 100;
        ws.Cell(3, 20).Value = "Wall";
        ws.Cell(3, 21).Value = "6 Sheets";

        ws.Cell(4, 1).Value = "Variant";
        ws.Cell(4, 2).Value = "CAL-2001";
        ws.Cell(4, 16).Value = "CAL-2001-B";
        ws.Cell(4, 17).Value = 2.5;
        ws.Cell(4, 18).Value = "TRUE";
        ws.Cell(4, 19).Value = 80;
        ws.Cell(4, 20).Value = "Wall";
        ws.Cell(4, 21).Value = "3 Sheets";

        ws.Row(1).Style.Font.Bold = true;
        ws.Columns().AdjustToContents();
        return Save(wb);
    }

    public async Task<byte[]> VariantSheetExportAsync(CancellationToken ct = default)
    {
        var products = await _db.Products
            .Where(p => p.TenantId == Tenant && !p.IsDeleted)
            .OrderBy(p => p.Category!.DisplayOrder).ThenBy(p => p.SortOrder).ThenBy(p => p.Name)
            .Select(p => new
            {
                p.ProductId, p.Sku, p.Name, Category = p.Category!.Name,
                Brand = p.Brand != null ? p.Brand.Name : "",
                p.Price, p.CompareAtPrice, p.CostPrice, p.HsnCode, p.Status, p.IsFeatured,
                ImageUrl = p.Images.OrderByDescending(i => i.IsPrimary).Select(i => i.Url).FirstOrDefault(),
            })
            .ToListAsync(ct);

        var productIds = products.Select(p => p.ProductId).ToList();
        var variants = await _db.ProductVariants.Include(v => v.Options)
            .Where(v => productIds.Contains(v.ProductId))
            .OrderBy(v => v.ProductVariantId)
            .ToListAsync(ct);
        var variantsByProduct = variants.GroupBy(v => v.ProductId).ToDictionary(g => g.Key, g => g.ToList());

        var variantIds = variants.Select(v => v.ProductVariantId).ToList();
        var stockByVariant = await _db.Inventory
            .Where(i => i.TenantId == Tenant && i.ProductVariantId != null && variantIds.Contains(i.ProductVariantId!.Value))
            .ToDictionaryAsync(i => i.ProductVariantId!.Value, i => i.AvailableQty, ct);

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Products & Variants");
        for (var i = 0; i < VariantSheetHeaders.Length; i++) ws.Cell(1, i + 1).Value = VariantSheetHeaders[i];

        var r = 2;
        foreach (var p in products)
        {
            var pVariants = variantsByProduct.TryGetValue(p.ProductId, out var vs) ? vs : [];
            // Option names, in first-seen order — the Product row declares what each of this
            // product's up-to-3 option slots means; the Variant rows below only carry values.
            var optionNames = pVariants.SelectMany(v => v.Options.Select(o => o.OptionName))
                .Distinct(StringComparer.OrdinalIgnoreCase).Take(3).ToList();

            ws.Cell(r, 1).Value = "Product";
            ws.Cell(r, 2).Value = p.Sku;
            ws.Cell(r, 3).Value = p.Name;
            ws.Cell(r, 4).Value = p.Category;
            ws.Cell(r, 5).Value = p.Brand;
            ws.Cell(r, 6).Value = p.Price;
            ws.Cell(r, 7).Value = p.CompareAtPrice;
            ws.Cell(r, 8).Value = p.CostPrice;
            ws.Cell(r, 9).Value = p.HsnCode;
            ws.Cell(r, 10).Value = p.Status;
            ws.Cell(r, 11).Value = p.IsFeatured;
            ws.Cell(r, 12).Value = p.ImageUrl;
            for (var i = 0; i < optionNames.Count; i++) ws.Cell(r, 13 + i).Value = optionNames[i];
            r++;

            foreach (var v in pVariants)
            {
                ws.Cell(r, 1).Value = "Variant";
                ws.Cell(r, 2).Value = p.Sku;
                ws.Cell(r, 16).Value = v.Sku;
                ws.Cell(r, 17).Value = v.PriceAdjustment;
                ws.Cell(r, 18).Value = v.IsActive;
                ws.Cell(r, 19).Value = stockByVariant.TryGetValue(v.ProductVariantId, out var qty) ? qty : 0;
                for (var i = 0; i < optionNames.Count; i++)
                {
                    var match = v.Options.FirstOrDefault(o => string.Equals(o.OptionName, optionNames[i], StringComparison.OrdinalIgnoreCase));
                    if (match is not null) ws.Cell(r, 20 + i).Value = match.OptionValue;
                }
                r++;
            }
        }
        ws.Row(1).Style.Font.Bold = true;
        return Save(wb);
    }

    public async Task<ImportResultDto> ImportVariantSheetAsync(Stream file, string? fileName, long? userId, CancellationToken ct = default)
    {
        var sheet = SheetReader.Read(file, fileName);
        if (sheet.Rows.Count == 0) throw new AppException("That file has no data rows.");

        var now = DateTime.UtcNow;
        var job = new ImportJob
        {
            TenantId = Tenant, JobType = "ProductVariants", FileName = fileName, Status = "Processing",
            StartedAt = now, CreatedBy = userId, CreatedAt = now,
        };
        _db.ImportJobs.Add(job);
        await _db.SaveChangesAsync(ct);

        var failed = new List<ImportJobItemDto>();
        int success = 0, fail = 0;
        long? currentProductId = null;
        string? currentProductSku = null;
        List<string> currentOptionNames = [];

        foreach (var row in sheet.Rows)
        {
            try
            {
                var sku = row.Get("SKU");
                if (sku.Length == 0) throw new InvalidOperationException("SKU is required on every row.");

                if (string.Equals(row.Get("RowType"), "Variant", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.Equals(sku, currentProductSku, StringComparison.OrdinalIgnoreCase))
                    {
                        // Not the product just processed from this file — look it up fresh, so a
                        // file holding only edited Variant rows for existing products (no repeated
                        // Product row above them) still works, not just a full grouped export.
                        var existing = await _db.Products.Include(p => p.Variants).ThenInclude(v => v.Options)
                            .FirstOrDefaultAsync(p => p.TenantId == Tenant && p.Sku == sku && !p.IsDeleted, ct)
                            ?? throw new InvalidOperationException($"No product with SKU '{sku}' — add a Product row for it above, or check the spelling.");
                        currentProductId = existing.ProductId;
                        currentProductSku = sku;
                        currentOptionNames = existing.Variants.SelectMany(v => v.Options.Select(o => o.OptionName))
                            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                    }
                    await ImportVariantRowAsync(row, currentProductId!.Value, currentOptionNames, userId, ct);
                }
                else
                {
                    currentProductId = await ImportVariantSheetProductRowAsync(row, now, ct);
                    currentProductSku = sku;
                    currentOptionNames = new[] { row.Get("Option1 Name"), row.Get("Option2 Name"), row.Get("Option3 Name") }
                        .Where(n => n.Length > 0).ToList();
                }

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
                    RawData = JsonSerializer.Serialize(new { sku = row.Get("SKU"), rowType = row.Get("RowType") }),
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

    /// <summary>
    /// The "Product" row of a group: upserts by SKU, same as the main template but with the
    /// smaller field set this sheet carries (no descriptions/SEO/sort order — use the main
    /// products template or the UI for those). Existing fields left off a row on an update are
    /// left untouched, not blanked.
    /// </summary>
    private async Task<long> ImportVariantSheetProductRowAsync(SheetRow row, DateTime now, CancellationToken ct)
    {
        var sku = row.Get("SKU");
        var name = row.Get("Name");

        var product = await _db.Products.FirstOrDefaultAsync(p => p.TenantId == Tenant && p.Sku == sku, ct);
        var isNew = product is null;
        if (isNew && name.Length == 0) throw new InvalidOperationException("Name is required for a new product.");
        product ??= new Product { TenantId = Tenant, Sku = sku, CreatedAt = now, IsActive = true };

        if (name.Length > 0) product.Name = name;

        var categoryName = row.Get("Category");
        if (categoryName.Length > 0)
        {
            var category = await GetOrCreateCategoryAsync(categoryName, now, ct);
            product.CategoryId = category.CategoryId;
        }
        else if (isNew) throw new InvalidOperationException("Category is required for a new product.");

        var priceText = row.Get("Price");
        if (priceText.Length > 0)
        {
            if (!decimal.TryParse(priceText, out var price) || price < 0) throw new InvalidOperationException($"Invalid price: '{priceText}'");
            product.Price = price;
        }
        else if (isNew) throw new InvalidOperationException("Price is required for a new product.");

        if (decimal.TryParse(row.Get("MRP"), out var mrp)) product.CompareAtPrice = mrp;
        if (decimal.TryParse(row.Get("CostPrice"), out var cost)) product.CostPrice = cost;
        var hsn = row.Get("HsnCode"); if (hsn.Length > 0) product.HsnCode = hsn;
        var status = row.Get("Status");
        if (status.Length > 0)
        {
            product.Status = status is "Active" or "Inactive" or "Draft" ? status : "Active";
            product.IsActive = product.Status == "Active";
        }
        var featured = row.Get("Featured");
        if (featured.Length > 0) product.IsFeatured = featured.Trim().ToLowerInvariant() is "true" or "yes" or "1";

        if (isNew)
        {
            product.Slug = await UniqueProductSlug(name, ct);
            _db.Products.Add(product);
        }
        else
        {
            product.UpdatedAt = now;
        }
        await _db.SaveChangesAsync(ct);

        var imageUrl = row.Get("ImageUrl");
        if (imageUrl.Length > 0)
        {
            var existingImages = await _db.ProductImages.Where(i => i.ProductId == product.ProductId).ToListAsync(ct);
            _db.ProductImages.RemoveRange(existingImages);
            _db.ProductImages.Add(new ProductImage { ProductId = product.ProductId, Url = imageUrl, IsPrimary = true, CreatedAt = now });
            await _db.SaveChangesAsync(ct);
        }

        return product.ProductId;
    }

    /// <summary>
    /// The "Variant" row of a group: upserts the variant by (product, SKU) — its own price
    /// difference, active flag, options and stock, exactly the per-variant detail the main
    /// one-row-per-product template can't carry.
    /// </summary>
    private async Task ImportVariantRowAsync(
        SheetRow row, long productId, IReadOnlyList<string> optionNames, long? userId, CancellationToken ct)
    {
        var variantSku = row.Get("Variant SKU");
        if (variantSku.Length == 0) throw new InvalidOperationException("Variant SKU is required on a Variant row.");

        var options = new List<VariantOptionDto>();
        for (var i = 0; i < optionNames.Count && i < 3; i++)
        {
            var value = row.Get($"Option{i + 1} Value");
            if (value.Length > 0) options.Add(new VariantOptionDto(optionNames[i], value));
        }

        // Blank/invalid Price Diff defaults to 0 — a variant with no override just matches the
        // product's base price.
        decimal.TryParse(row.Get("Price Diff"), out var priceDiff);

        var activeText = row.Get("Active");
        var active = activeText.Length == 0 || activeText.Trim().ToLowerInvariant() is "true" or "yes" or "1";

        var name = options.Count > 0 ? string.Join(" / ", options.Select(o => o.OptionValue)) : null;

        var existing = await _db.ProductVariants
            .FirstOrDefaultAsync(v => v.ProductId == productId && v.Sku == variantSku, ct);

        var req = new SaveVariantRequest(variantSku, name, priceDiff, active, options);
        var variantId = existing is null
            ? (await _variants.CreateAsync(productId, req, ct))!.ProductVariantId
            : (await _variants.UpdateAsync(productId, existing.ProductVariantId, req, ct))!.ProductVariantId;

        var stockText = row.Get("Stock");
        if (int.TryParse(stockText, out var stock))
        {
            // The sheet has no Reorder Level column — carry the existing one forward instead of
            // silently zeroing it out, since SetStockAsync replaces both fields wholesale.
            var reorderLevel = await _db.Inventory
                .Where(i => i.TenantId == Tenant && i.ProductVariantId == variantId)
                .Select(i => (int?)i.ReorderLevel).FirstOrDefaultAsync(ct) ?? 0;
            await _inventory.SetVariantStockAsync(productId, variantId, new SetStockRequest(stock, reorderLevel), userId, ct);
        }
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
        var description = row.Get("Description"); if (description.Length > 0) product.Description = description;
        var metaTitle = row.Get("MetaTitle"); if (metaTitle.Length > 0) product.MetaTitle = metaTitle;
        var metaDesc = row.Get("MetaDescription"); if (metaDesc.Length > 0) product.MetaDescription = metaDesc;
        var metaKeywords = FirstNonEmpty(row, "MetaKeywords", "Keywords"); if (metaKeywords.Length > 0) product.MetaKeywords = metaKeywords;
        var featured = row.Get("Featured");
        if (featured.Length > 0) product.IsFeatured = featured.Trim().ToLowerInvariant() is "true" or "yes" or "1";

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
        await ApplyOptionColumnsAsync(row, product.ProductId, ct);
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

    /// <summary>
    /// Generates variants from a row's Option1-3 Name/Values columns — the same cartesian-
    /// product generator behind the "Generate variants" button on the product edit page, so a
    /// bulk import defines exactly what that screen would, and it's additive there for the
    /// same reason: a value added to an option and re-imported only creates the new
    /// combinations, never disturbing SKUs/prices/stock already set on existing ones. A row
    /// with no Option columns filled in touches no variants at all.
    /// </summary>
    private async Task ApplyOptionColumnsAsync(SheetRow row, long productId, CancellationToken ct)
    {
        var groups = new List<GenerateVariantsOptionInput>();
        for (var i = 1; i <= 3; i++)
        {
            var name = row.Get($"Option{i} Name");
            var valuesText = row.Get($"Option{i} Values");
            if (name.Length == 0 || valuesText.Length == 0) continue;

            var values = valuesText.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (values.Length > 0) groups.Add(new GenerateVariantsOptionInput(name, values));
        }
        if (groups.Count == 0) return;

        await _variants.GenerateAsync(productId, new GenerateVariantsRequest(groups), ct);
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
