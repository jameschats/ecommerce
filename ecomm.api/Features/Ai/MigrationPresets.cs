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

    public static readonly IReadOnlyList<MigrationPreset> All = new[] { Shopify, WooCommerce, Wix };

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
