using System.Text.Json;

namespace ecomm.api.Features.Storefront;

/// <summary>One section in a prebuilt theme template: a type + ready-made settings/blocks JSON.</summary>
public sealed record PrebuiltSection(string Type, string? Title, string? Settings, string? Blocks);

/// <summary>One page-type template (or header/footer/announcement zone) within a prebuilt theme.</summary>
public sealed record PrebuiltTemplate(string TemplateKey, IReadOnlyList<PrebuiltSection> Sections);

/// <summary>A full free prebuilt theme bundle: global settings + a set of templates.</summary>
public sealed record PrebuiltTheme(
    string Key, string Name, string Category, string Description,
    IReadOnlyDictionary<string, string> Settings, IReadOnlyList<PrebuiltTemplate> Templates);

/// <summary>Install-picker summary (palette drives the thumbnail; no section payloads).</summary>
public sealed record PrebuiltThemeSummary(
    string Key, string Name, string Category, string Description, string PrimaryColor, string SecondaryColor, string Font);

/// <summary>
/// The catalog of free prebuilt themes a merchant can install into their library (S6) — our answer to a
/// paid theme store. Each is a self-contained bundle: global settings (palette/typography/buttons) + a
/// home (<c>index</c>) layout + Header/Footer/Announcement zones, composed only from the central section
/// catalog. Installing copies a bundle into the tenant's library as a Draft to preview then publish.
/// </summary>
public static class PrebuiltThemeRegistry
{
    public static readonly IReadOnlyList<PrebuiltTheme> All =
    [
        Theme("minimal", "Minimal", "General", "Clean, neutral and content-first — works for any store.",
            palette: ("#111827", "#6b7280", "Inter", "rounded"),
            announce: "Free shipping on orders over ₹499",
            index:
            [
                Hero("Welcome to our store", "Great products, fair prices, fast delivery.", "Shop now"),
                Featured("Featured", "featured"),
                Categories("Shop by category"),
                Testimonials(),
            ]),

        Theme("boutique", "Boutique", "Fashion", "Bold hero and editorial feel for apparel and accessories.",
            palette: ("#d6336c", "#212529", "Poppins", "pill"),
            announce: "New season drops every week ✨",
            index:
            [
                Hero("New season, new you", "Fresh drops every week — find your look.", "Shop new in"),
                Categories("Shop by category"),
                Featured("New arrivals", "newest"),
                ImageWithText("Made to last", "Thoughtfully designed, ethically made. Quality you can feel in every stitch."),
                Testimonials(),
                Cta("Join our list", "Get 10% off your first order.", "Sign up", "#212529"),
            ]),

        Theme("circuit", "Circuit", "Electronics", "Sharp, techy layout that leads with deals and best-sellers.",
            palette: ("#2563eb", "#0f172a", "Inter", "square"),
            announce: "Members get early access to every launch",
            index:
            [
                Hero("Tech that keeps up", "The latest gear at prices that make sense.", "Shop deals"),
                Categories("Browse categories"),
                Featured("Best sellers", "bestsellers"),
                Cta("Save on bundles", "Members get early access to launches and offers.", "Become a member", "#0f172a"),
            ]),

        Theme("fresh", "Fresh", "Grocery", "Aisle-first layout with a delivery promo for everyday essentials.",
            palette: ("#16a34a", "#166534", "Inter", "rounded"),
            announce: "Fresh to your door — free delivery over ₹499",
            index:
            [
                Categories("Shop by aisle"),
                Featured("Today's picks", "featured"),
                Cta("Free delivery over ₹499", "Fresh to your door, fast.", "Start shopping", "#166534"),
            ]),

        Theme("bloom", "Bloom", "Beauty", "Soft, elegant look for beauty, skincare and wellness.",
            palette: ("#db2777", "#7c3aed", "Poppins", "pill"),
            announce: "Complimentary samples with every order 🌸",
            index:
            [
                Hero("Glow, naturally", "Clean beauty, made to make you feel good.", "Discover"),
                Categories("Shop by category"),
                Featured("Bestsellers", "bestsellers"),
                ImageWithText("Kind to you and the planet", "Cruelty-free, dermatologically tested, thoughtfully packaged."),
                Cta("Get 10% off", "Join for tips, launches and a welcome treat.", "Join Bloom", "#7c3aed"),
            ]),

        Theme("haven", "Haven", "Home & Living", "Warm, homely layout for furniture, decor and living.",
            palette: ("#a16207", "#44403c", "Inter", "rounded"),
            announce: "Free assembly on select furniture",
            index:
            [
                Hero("Make it home", "Pieces you'll love for years, at honest prices.", "Shop the look"),
                Categories("Shop by room"),
                Featured("New in", "newest"),
                ImageWithText("Built to last", "Solid materials, timeless design — furniture that grows with you."),
                Testimonials(),
            ]),
    ];

    public static IReadOnlyList<PrebuiltThemeSummary> Summaries =>
        All.Select(t => new PrebuiltThemeSummary(
            t.Key, t.Name, t.Category, t.Description,
            t.Settings.GetValueOrDefault("PrimaryColor", "#111827"),
            t.Settings.GetValueOrDefault("SecondaryColor", "#6b7280"),
            t.Settings.GetValueOrDefault("Font", "Inter"))).ToList();

    public static PrebuiltTheme? Get(string key) =>
        All.FirstOrDefault(t => string.Equals(t.Key, key, StringComparison.OrdinalIgnoreCase));

    // --- bundle builder ---
    private static PrebuiltTheme Theme(
        string key, string name, string category, string description,
        (string primary, string secondary, string font, string button) palette,
        string announce, IReadOnlyList<PrebuiltSection> index) =>
        new(key, name, category, description,
            new Dictionary<string, string>
            {
                ["PrimaryColor"] = palette.primary,
                ["SecondaryColor"] = palette.secondary,
                ["Font"] = palette.font,
                ["ButtonStyle"] = palette.button,
            },
            [
                new("index", index),
                new("announcement", [Announcement(announce, palette.secondary)]),
                new("header", [Header()]),
                new("footer", [Footer($"© {name}. All rights reserved.")]),
            ]);

    // --- section builders (valid settings/blocks JSON against the central section-type catalog) ---
    private static PrebuiltSection Hero(string heading, string sub, string cta) => new(
        "Hero", "Hero", """{"autoplay":true,"intervalSec":5}""",
        $$"""[{"image":"","heading":{{J(heading)}},"subheading":{{J(sub)}},"buttonText":{{J(cta)}},"buttonLink":"/products"}]""");

    private static PrebuiltSection Categories(string heading) => new(
        "Categories", "Category strip", $$"""{"heading":{{J(heading)}},"style":"grid"}""", "[]");

    private static PrebuiltSection Featured(string heading, string source) => new(
        "FeaturedProducts", "Featured products", $$"""{"heading":{{J(heading)}},"source":{{J(source)}},"count":8,"columns":4}""", "[]");

    private static PrebuiltSection ImageWithText(string heading, string body) => new(
        "ImageWithText", "Image with text",
        $$"""{"image":"","imageSide":"left","heading":{{J(heading)}},"body":{{J(body)}},"buttonText":"Learn more","buttonLink":"/products"}""", "[]");

    private static PrebuiltSection Testimonials() => new(
        "Testimonials", "Testimonials", """{"heading":"What customers say"}""",
        """[{"quote":"Great quality and quick delivery. Highly recommend!","author":"Happy customer","rating":5},{"quote":"Exactly as described. Will order again.","author":"Verified buyer","rating":5}]""");

    private static PrebuiltSection Cta(string heading, string sub, string cta, string bg) => new(
        "CtaNewsletter", "Call to action",
        $$"""{"heading":{{J(heading)}},"subtext":{{J(sub)}},"buttonText":{{J(cta)}},"buttonLink":"/products","backgroundColor":{{J(bg)}}}""", "[]");

    private static PrebuiltSection Announcement(string message, string bg) => new(
        "AnnouncementBar", "Announcement bar", $$"""{"backgroundColor":{{J(bg)}},"autoplay":true}""",
        $$"""[{"text":{{J(message)}},"link":"/products"}]""");

    private static PrebuiltSection Header() => new(
        "Header", "Header", """{"showSearch":true,"showCart":true,"sticky":true,"menuHandle":"main"}""", "[]");

    private static PrebuiltSection Footer(string copyright) => new(
        "Footer", "Footer", $$"""{"showPolicies":true,"copyright":{{J(copyright)}}}""", "[]");

    private static string J(string s) => JsonSerializer.Serialize(s);
}
