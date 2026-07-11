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
            ], headingFont: "Inter", radius: "soft", card: "shadow", density: "cozy"),

        // Flagship electronics theme (Phase B) — image banner + USP row + category cards + bestseller carousel.
        Theme("ignition", "Ignition", "Electronics", "Bold, deal-led electronics store — image banner, USP row, category cards and a bestseller carousel.",
            palette: ("#2563eb", "#0b1220", "Inter", "square"),
            announce: "Free shipping over ₹499 · Members get early access to every launch",
            index:
            [
                HeroBanner("Upgrade your tech", "The latest gear at prices that make sense — with fast, free delivery.", "Shop deals",
                    "https://images.unsplash.com/photo-1517336714731-489689fd1ca8?auto=format&fit=crop&w=1600&q=80"),
                Multicolumn("Why shop with us",
                    ("🚚", "Free shipping", "On orders over ₹499"),
                    ("🛡️", "2-year warranty", "On all devices"),
                    ("↩️", "30-day returns", "No questions asked"),
                    ("💬", "24/7 support", "We're here to help")),
                Categories("Shop by category", "cards"),
                Featured("Best sellers", "bestsellers", "carousel"),
                Cta("Members save more", "Early access to launches and member-only deals.", "Become a member", "#0b1220"),
                Featured("New arrivals", "newest"),
            ], headingFont: "Space Grotesk", radius: "sharp", card: "elevated", density: "compact"),

        Theme("boutique", "Boutique", "Fashion", "Bold hero and editorial feel for apparel and accessories.",
            palette: ("#d6336c", "#212529", "Poppins", "pill"),
            announce: "New season drops every week ✨",
            index:
            [
                HeroBanner("New season, new you", "Fresh drops every week — discover your next favourite look.", "Shop new in",
                    "https://images.unsplash.com/photo-1441984904996-e0b6ba687e04?auto=format&fit=crop&w=1600&q=80"),
                Multicolumn("The boutique promise",
                    ("🚚", "Free shipping", "On orders over ₹499"),
                    ("↩️", "Easy returns", "30-day, no fuss"),
                    ("🧵", "Made to last", "Quality in every stitch"),
                    ("💬", "Style help", "Here whenever you need")),
                Categories("Shop by category", "cards"),
                Featured("New arrivals", "newest", "carousel"),
                ImageWithText("Made to last", "Thoughtfully designed, ethically made. Quality you can feel in every stitch."),
                Featured("Trending now", "bestsellers"),
                Cta("Join our list", "Get 10% off your first order.", "Sign up", "#212529"),
            ], headingFont: "Playfair Display", radius: "round", card: "shadow", density: "spacious"),

        Theme("circuit", "Circuit", "Electronics", "Sharp, techy layout that leads with deals and best-sellers.",
            palette: ("#2563eb", "#0f172a", "Inter", "square"),
            announce: "Members get early access to every launch",
            index:
            [
                HeroBanner("Tech that keeps up", "The latest gear at prices that make sense — with fast, free delivery.", "Shop deals",
                    "https://images.unsplash.com/photo-1498049794561-7780e7231661?auto=format&fit=crop&w=1600&q=80"),
                Multicolumn("Why shop with us",
                    ("🚚", "Free shipping", "On orders over ₹499"),
                    ("🛡️", "2-year warranty", "On all devices"),
                    ("↩️", "30-day returns", "No questions asked"),
                    ("⚡", "Fast support", "Real humans, 24/7")),
                Categories("Browse categories", "cards"),
                Featured("Best sellers", "bestsellers", "carousel"),
                Cta("Save on bundles", "Members get early access to launches and offers.", "Become a member", "#0f172a"),
                Featured("New arrivals", "newest"),
            ], headingFont: "Space Grotesk", radius: "sharp", card: "elevated", density: "compact"),

        Theme("fresh", "Fresh", "Grocery", "Aisle-first layout with a delivery promo for everyday essentials.",
            palette: ("#16a34a", "#166534", "Inter", "rounded"),
            announce: "Fresh to your door — free delivery over ₹499",
            index:
            [
                HeroBanner("Fresh to your door", "Everyday essentials and just-picked produce — delivered fast.", "Start shopping",
                    "https://images.unsplash.com/photo-1542838132-92c53300491e?auto=format&fit=crop&w=1600&q=80"),
                Multicolumn("Groceries made easy",
                    ("🚚", "Free delivery", "On orders over ₹499"),
                    ("🥬", "Farm fresh", "Sourced daily"),
                    ("⏰", "Same-day slots", "Order by noon"),
                    ("💳", "Secure checkout", "Pay your way")),
                Categories("Shop by aisle", "cards"),
                Featured("Today's picks", "featured", "carousel"),
                Cta("Free delivery over ₹499", "Fresh to your door, fast.", "Start shopping", "#166534"),
                Featured("New this week", "newest"),
            ], headingFont: "Nunito", radius: "round", card: "shadow", density: "cozy"),

        Theme("bloom", "Bloom", "Beauty", "Soft, elegant look for beauty, skincare and wellness.",
            palette: ("#db2777", "#7c3aed", "Poppins", "pill"),
            announce: "Complimentary samples with every order 🌸",
            index:
            [
                HeroBanner("Glow, naturally", "Clean beauty made to make you feel good — inside and out.", "Discover",
                    "https://images.unsplash.com/photo-1596462502278-27bfdc403348?auto=format&fit=crop&w=1600&q=80"),
                Multicolumn("The Bloom difference",
                    ("🌸", "Clean formulas", "Kind to skin"),
                    ("🐰", "Cruelty-free", "Always"),
                    ("🎁", "Free samples", "With every order"),
                    ("↩️", "Easy returns", "30-day promise")),
                Categories("Shop by category", "cards"),
                Featured("Bestsellers", "bestsellers", "carousel"),
                ImageWithText("Kind to you and the planet", "Cruelty-free, dermatologically tested, thoughtfully packaged."),
                Cta("Get 10% off", "Join for tips, launches and a welcome treat.", "Join Bloom", "#7c3aed"),
            ], headingFont: "Cormorant Garamond", radius: "round", card: "flat", density: "spacious"),

        Theme("haven", "Haven", "Home & Living", "Warm, homely layout for furniture, decor and living.",
            palette: ("#a16207", "#44403c", "Inter", "rounded"),
            announce: "Free assembly on select furniture",
            index:
            [
                HeroBanner("Make it home", "Pieces you'll love for years, at honest prices.", "Shop the look",
                    "https://images.unsplash.com/photo-1616486338812-3dadae4b4ace?auto=format&fit=crop&w=1600&q=80"),
                Multicolumn("Why Haven",
                    ("🚚", "Free shipping", "On orders over ₹499"),
                    ("🛠️", "Free assembly", "On select furniture"),
                    ("🌳", "Built to last", "Solid, timeless design"),
                    ("↩️", "Easy returns", "30-day peace of mind")),
                Categories("Shop by room", "cards"),
                Featured("New in", "newest", "carousel"),
                ImageWithText("Built to last", "Solid materials, timeless design — furniture that grows with you."),
                Featured("Bestsellers", "bestsellers"),
            ], headingFont: "Lora", radius: "soft", card: "bordered", density: "spacious"),
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
        string announce, IReadOnlyList<PrebuiltSection> index,
        string? headingFont = null, string radius = "soft", string card = "bordered",
        string density = "cozy", string headingCase = "none") =>
        new(key, name, category, description,
            new Dictionary<string, string>
            {
                ["PrimaryColor"] = palette.primary,
                ["SecondaryColor"] = palette.secondary,
                ["AccentColor"] = palette.primary,
                ["Font"] = palette.font,
                ["HeadingFont"] = headingFont ?? palette.font,
                ["ButtonStyle"] = palette.button,
                ["Radius"] = radius,
                ["CardStyle"] = card,
                ["Density"] = density,
                ["HeadingTransform"] = headingCase,
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

    /// <summary>A full-width image banner hero (style=banner) with a background image.</summary>
    private static PrebuiltSection HeroBanner(string heading, string sub, string cta, string image) => new(
        "Hero", "Hero", """{"style":"banner","autoplay":false}""",
        $$"""[{"image":{{J(image)}},"heading":{{J(heading)}},"subheading":{{J(sub)}},"buttonText":{{J(cta)}},"buttonLink":"/products"}]""");

    /// <summary>A row of icon + heading + text tiles (USPs / how-it-works).</summary>
    private static PrebuiltSection Multicolumn(string? heading, params (string icon, string heading, string text)[] cols)
    {
        var blocks = string.Join(",", cols.Select(c => $$"""{"icon":{{J(c.icon)}},"heading":{{J(c.heading)}},"text":{{J(c.text)}}}"""));
        return new("Multicolumn", "Feature columns", $$"""{"heading":{{J(heading ?? "")}}}""", $"[{blocks}]");
    }

    private static PrebuiltSection Categories(string heading, string style = "grid") => new(
        "Categories", "Category strip", $$"""{"heading":{{J(heading)}},"style":{{J(style)}}}""", "[]");

    private static PrebuiltSection Featured(string heading, string source, string layout = "grid") => new(
        "FeaturedProducts", "Featured products", $$"""{"heading":{{J(heading)}},"layout":{{J(layout)}},"source":{{J(source)}},"count":8,"columns":4}""", "[]");

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
