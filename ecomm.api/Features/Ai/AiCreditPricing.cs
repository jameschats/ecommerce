namespace ecomm.api.Features.Ai;

/// <summary>
/// Abstract, predictable per-action credit costs (the platform absorbs token variance). Keep these in
/// step with the buy-credits packs so a plan's monthly grant + top-ups stay profitable — track the real
/// provider spend via <c>AiUsageLog.CostMicros</c> and re-tune.
/// </summary>
public static class AiCreditPricing
{
    public const string ImproveText = "improve-text";
    public const string Seo = "seo";
    public const string Category = "category";
    public const string ColumnMap = "column-map";
    public const string Page = "page";
    public const string SampleCatalog = "sample-catalog";
    public const string SupportDraft = "support-draft";

    // AI Growth — marketing content (G1). Costs scale with output length; text is cheap enough
    // (~₹0.04 a call at gpt-4.1-mini rates) that these are priced for daily use, not rationing.
    public const string GrowthInstagram = "growth-instagram";
    public const string GrowthFacebook = "growth-facebook";
    public const string GrowthWhatsapp = "growth-whatsapp";
    public const string GrowthEmail = "growth-email";
    public const string GrowthProductDescription = "growth-product-description";
    public const string GrowthFestival = "growth-festival";
    public const string GrowthGoogleAds = "growth-google-ads";

    private static readonly IReadOnlyDictionary<string, int> Costs = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        [ImproveText] = 1,
        [Seo] = 1,
        [Category] = 1,
        [ColumnMap] = 2,
        // A support draft reads the thread, the linked order and the FAQ corpus, so its prompt is
        // larger than a rewrite — but it saves a merchant a real reply, so keep it cheap enough to use daily.
        [SupportDraft] = 2,
        [Page] = 5,
        [SampleCatalog] = 25,
        [GrowthWhatsapp] = 2,
        [GrowthInstagram] = 3,
        [GrowthFacebook] = 3,
        [GrowthFestival] = 3,
        [GrowthGoogleAds] = 5,
        [GrowthProductDescription] = 5,
        [GrowthEmail] = 8,
    };

    /// <summary>Credit cost of a feature (defaults to 1 for anything unlisted).</summary>
    public static int CostOf(string feature) => Costs.TryGetValue(feature, out var c) ? c : 1;
}
