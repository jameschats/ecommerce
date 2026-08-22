namespace ecomm.api.Features.Ai;

/// <summary>
/// Known column layouts for the big platforms' product exports (AI-4). A preset maps the platform's
/// recognised headers to our schema deterministically; any column it doesn't know falls through to AI
/// mapping. <see cref="Detect"/> guesses the platform from the header row; the merchant can override it.
/// </summary>
public sealed record MigrationPreset(
    string Key, string Label, IReadOnlyList<string> Signature, IReadOnlyDictionary<string, string> Map);

public static class MigrationPresets
{
    private static Dictionary<string, string> Ci(params (string Header, string Target)[] pairs)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (h, t) in pairs) d[h] = t;
        return d;
    }

    private static readonly MigrationPreset Shopify = new("shopify", "Shopify",
        new[] { "Handle", "Body (HTML)", "Variant SKU", "Variant Price", "Image Src" },
        Ci(
            ("Handle", "ignore"), ("Title", "name"), ("Body (HTML)", "description"), ("Vendor", "brand"),
            ("Type", "category"), ("Product Category", "ignore"), ("Tags", "ignore"), ("Published", "ignore"),
            ("Variant SKU", "sku"), ("Variant Price", "price"), ("Variant Compare At Price", "compareatprice"),
            ("Cost per item", "costprice"), ("Variant Grams", "ignore"), ("Variant Inventory Qty", "ignore"),
            ("Variant Inventory Tracker", "ignore"), ("Variant Inventory Policy", "ignore"),
            ("Variant Fulfillment Service", "ignore"), ("Variant Requires Shipping", "ignore"),
            ("Variant Taxable", "ignore"), ("Variant Barcode", "ignore"), ("Variant Weight Unit", "ignore"),
            ("Image Src", "imageurl"), ("Image Position", "ignore"), ("Image Alt Text", "ignore"),
            ("Gift Card", "ignore"), ("SEO Title", "ignore"), ("SEO Description", "ignore"), ("Status", "status"),
            ("Option1 Name", "ignore"), ("Option1 Value", "ignore"), ("Option2 Name", "ignore"),
            ("Option2 Value", "ignore"), ("Option3 Name", "ignore"), ("Option3 Value", "ignore")));

    private static readonly MigrationPreset WooCommerce = new("woocommerce", "WooCommerce",
        new[] { "Regular price", "Categories", "Images", "Short description", "In stock?" },
        Ci(
            ("ID", "ignore"), ("Type", "ignore"), ("SKU", "sku"), ("Name", "name"), ("Published", "ignore"),
            ("Is featured?", "ignore"), ("Visibility in catalog", "ignore"), ("Short description", "shortdescription"),
            ("Description", "description"), ("Regular price", "price"), ("Sale price", "ignore"),
            ("Categories", "category"), ("Tags", "ignore"), ("In stock?", "ignore"), ("Stock", "ignore"),
            ("Images", "imageurl"), ("Tax status", "ignore"), ("Tax class", "ignore"), ("Weight (kg)", "ignore"),
            ("Length (cm)", "ignore"), ("Width (cm)", "ignore"), ("Height (cm)", "ignore"), ("Brands", "brand")));

    private static readonly MigrationPreset Wix = new("wix", "Wix",
        new[] { "handleId", "fieldType", "productImageUrl", "collection" },
        Ci(
            ("handleId", "ignore"), ("fieldType", "ignore"), ("name", "name"), ("description", "description"),
            ("productImageUrl", "imageurl"), ("collection", "category"), ("sku", "sku"), ("ribbon", "ignore"),
            ("price", "price"), ("surcharge", "ignore"), ("visible", "ignore"), ("discountMode", "ignore"),
            ("discountValue", "ignore"), ("inventory", "ignore"), ("weight", "ignore"), ("cost", "costprice"),
            ("brand", "brand")));

    // Confident: sourced from Zoho Commerce's own published export-column documentation
    // (zoho.com/commerce/api/export-products.html) — Product Name, Store/Long Description,
    // Selling/Label Price, Category URL, SEO fields, EAN/UPC/ISBN, and the Attribute1-3 columns
    // for variants are all exactly as Zoho's own docs describe them.
    private static readonly MigrationPreset ZohoCommerce = new("zoho", "Zoho Commerce",
        new[] { "Store Description", "Selling Price", "Qualifies For Returns", "Label Price" },
        Ci(
            ("Product ID", "ignore"), ("Handle", "ignore"), ("Product Name", "name"),
            ("Store Description", "shortdescription"), ("Long Description", "description"), ("Brand", "brand"),
            ("SKU", "sku"), ("Selling Price", "price"), ("Label Price", "compareatprice"),
            ("Category URL", "category"), ("Tags", "ignore"), ("Item Type", "ignore"), ("On Sale", "ignore"),
            ("Show In Store", "ignore"), ("Qualifies For Returns", "ignore"), ("Opening Stock", "ignore"),
            ("Reorder Level", "ignore"), ("Variant ID", "ignore"), ("Part Number", "ignore"),
            ("EAN", "ignore"), ("UPC", "ignore"), ("ISBN", "ignore"), ("Avalara Tax Code", "ignore"),
            ("SEO Title", "ignore"), ("SEO Description", "ignore"), ("SEO Keyword", "ignore"),
            ("Package Height", "ignore"), ("Package Length", "ignore"), ("Package Width", "ignore"), ("Package Weight", "ignore"),
            ("AttributeName1", "ignore"), ("AttributeOption1", "ignore"), ("AttributeType1", "ignore"),
            ("AttributeName2", "ignore"), ("AttributeOption2", "ignore"), ("AttributeType2", "ignore"),
            ("AttributeName3", "ignore"), ("AttributeOption3", "ignore"), ("AttributeType3", "ignore")));

    // Best-effort, NOT verified against a real export file — Dukaan's and Instamojo's own bulk-
    // upload templates aren't published in a way this could be confirmed against. Two distinct risks
    // this carries, both bounded by the existing review-before-write flow (nothing is ever written
    // without the merchant seeing and correcting the proposed mapping first): (1) a wrong header
    // guess simply never matches a real column and is inert, same as an unlisted platform; (2) the
    // signature lists below lean on fairly generic column names ("Product Name", "Price"), which
    // could false-positive auto-detect against a genuinely different, unlisted platform's export —
    // worse UX (wrong preset pre-selected) but not data-corrupting, since the merchant still reviews
    // and corrects before Apply. Confirm the exact column names against a real export the first time
    // either of these actually gets used, and correct this preset then.
    private static readonly MigrationPreset Dukaan = new("dukaan", "Dukaan",
        new[] { "HSN Code", "Product Name", "Selling Price" },
        Ci(
            ("Product Name", "name"), ("Description", "description"), ("Category", "category"),
            ("Selling Price", "price"), ("MRP", "compareatprice"), ("SKU", "sku"), ("HSN Code", "ignore"),
            ("Stock", "ignore"), ("Image", "imageurl"), ("Image URL", "imageurl"), ("Brand", "brand"),
            ("SEO title", "ignore"), ("SEO description", "ignore")));

    private static readonly MigrationPreset Instamojo = new("instamojo", "Instamojo",
        new[] { "Product Name", "Price", "Category" },
        Ci(
            ("Product Name", "name"), ("Description", "description"), ("Category", "category"),
            ("Price", "price"), ("SKU", "sku"), ("Stock", "ignore"), ("Image URL", "imageurl"),
            ("Quantity", "ignore"), ("Weight", "ignore")));

    public static readonly IReadOnlyList<MigrationPreset> All = new[] { Shopify, WooCommerce, Wix, ZohoCommerce, Dukaan, Instamojo };

    /// <summary>Best-matching platform for a header row (≥2 signature columns present), or null.</summary>
    public static MigrationPreset? Detect(IReadOnlyList<string> headers)
    {
        var set = new HashSet<string>(headers, StringComparer.OrdinalIgnoreCase);
        return All
            .Select(p => (Preset: p, Hits: p.Signature.Count(set.Contains)))
            .Where(x => x.Hits >= 2)
            .OrderByDescending(x => x.Hits)
            .Select(x => x.Preset)
            .FirstOrDefault();
    }

    public static MigrationPreset? Get(string? key) =>
        string.IsNullOrWhiteSpace(key) ? null : All.FirstOrDefault(p => p.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
}
