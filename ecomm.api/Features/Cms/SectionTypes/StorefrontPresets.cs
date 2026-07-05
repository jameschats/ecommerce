namespace ecomm.api.Features.Cms.SectionTypes;

/// <summary>One section in a preset: a type + ready-made settings/blocks JSON.</summary>
public sealed record PresetSection(string Type, string? Settings, string? Blocks);

/// <summary>A per-industry starter home layout (a pre-arranged set of sections).</summary>
public sealed record StorefrontPreset(string Key, string Label, string Description, IReadOnlyList<PresetSection> Sections);

/// <summary>Short summary for the picker (no section payloads).</summary>
public sealed record PresetSummary(string Key, string Label, string Description);

/// <summary>
/// Industry starter presets — a merchant picks one and their Home page is filled with a
/// sensible arrangement of sections they can then tweak. Ties to onboarding's "pick your
/// industry". Presets only reference the central section-type catalog; no merchant code.
/// </summary>
public static class StorefrontPresets
{
    public static readonly IReadOnlyList<StorefrontPreset> All = new List<StorefrontPreset>
    {
        new("fashion", "Fashion & apparel", "Big hero, category tiles, new-in rail, story block, reviews.",
        [
            Hero("New season, new you", "Fresh drops every week — find your look.", "Shop new in"),
            Categories("Shop by category"),
            Featured("New arrivals", "newest"),
            ImageWithText("Made to last", "Thoughtfully designed, ethically made. Quality you can feel in every stitch."),
            Testimonials(),
            Cta("Join our list", "Get 10% off your first order.", "Sign up"),
        ]),

        new("electronics", "Electronics & gadgets", "Hero, categories, best-sellers, promo band.",
        [
            Hero("Tech that keeps up", "The latest gear at prices that make sense.", "Shop deals"),
            Categories("Browse categories"),
            Featured("Best sellers", "bestsellers"),
            Cta("Save on bundles", "Members get early access to launches and offers.", "Become a member"),
        ]),

        new("grocery", "Grocery & essentials", "Categories first, featured picks, delivery promo.",
        [
            Categories("Shop by aisle"),
            Featured("Today's picks", "featured"),
            Cta("Free delivery over ₹499", "Fresh to your door, fast.", "Start shopping"),
        ]),

        new("general", "General store", "A balanced starter: hero, featured, categories, reviews.",
        [
            Hero("Welcome to our store", "Great products, fair prices, fast delivery.", "Shop now"),
            Featured("Featured", "featured"),
            Categories("Shop by category"),
            Testimonials(),
        ]),
    };

    public static IReadOnlyList<PresetSummary> Summaries =>
        All.Select(p => new PresetSummary(p.Key, p.Label, p.Description)).ToList();

    public static StorefrontPreset? Get(string key) =>
        All.FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase));

    // --- builders (produce valid settings/blocks JSON against the section-type catalog) ---
    private static PresetSection Hero(string heading, string sub, string cta) => new(
        "Hero",
        """{"autoplay":true,"intervalSec":5}""",
        $$"""[{"image":"","heading":{{J(heading)}},"subheading":{{J(sub)}},"buttonText":{{J(cta)}},"buttonLink":"/products"}]""");

    private static PresetSection Categories(string heading) => new(
        "Categories", $$"""{"heading":{{J(heading)}},"style":"grid"}""", "[]");

    private static PresetSection Featured(string heading, string source) => new(
        "FeaturedProducts", $$"""{"heading":{{J(heading)}},"source":{{J(source)}},"count":8,"columns":4}""", "[]");

    private static PresetSection ImageWithText(string heading, string body) => new(
        "ImageWithText",
        $$"""{"image":"","imageSide":"left","heading":{{J(heading)}},"body":{{J(body)}},"buttonText":"Learn more","buttonLink":"/products"}""",
        "[]");

    private static PresetSection Testimonials() => new(
        "Testimonials", """{"heading":"What customers say"}""",
        """[{"quote":"Great quality and quick delivery. Highly recommend!","author":"Happy customer","rating":5},{"quote":"Exactly as described. Will order again.","author":"Verified buyer","rating":5}]""");

    private static PresetSection Cta(string heading, string sub, string cta) => new(
        "CtaNewsletter",
        $$"""{"heading":{{J(heading)}},"subtext":{{J(sub)}},"buttonText":{{J(cta)}},"buttonLink":"/products","backgroundColor":"#111827"}""",
        "[]");

    /// <summary>JSON-encode a string literal (quotes + escaping) for embedding in the JSON above.</summary>
    private static string J(string s) => System.Text.Json.JsonSerializer.Serialize(s);
}
