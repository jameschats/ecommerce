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
            ], headingFont: "Inter", radius: "soft", card: "shadow", density: "cozy",
               headerLayout: "minimal", footerLayout: "simple"),

        // Electronics superstore (Maximize-inspired): dark hero, savings tiles, department tiles, dense rails.
        Theme("ignition", "Ignition", "Electronics", "Deal-led electronics superstore — dark hero, savings tiles and dense product rails.",
            palette: ("#2563eb", "#0b1220", "Inter", "square"),
            announce: "Free shipping over ₹499 · Members get early access to every launch",
            index:
            [
                HeroBanner("Sound. Vision. Power.", "Flagship tech at prices that make sense — fast, free delivery included.", "Shop deals",
                    "https://images.unsplash.com/photo-1550009158-9ebf69173e03?auto=format&fit=crop&w=1600&q=80"),
                PromoTiles("Today's top deals",
                    ("Save up to 35%", "Headphones & audio", "https://images.unsplash.com/photo-1505740420928-5e560c06d30e?auto=format&fit=crop&w=800&q=80"),
                    ("New drop", "Smart watches", "https://images.unsplash.com/photo-1523275335684-37898b6baf30?auto=format&fit=crop&w=800&q=80"),
                    ("Save big", "Speakers & home audio", "https://images.unsplash.com/photo-1608043152269-423dbba4e7e1?auto=format&fit=crop&w=800&q=80")),
                TileGrid("Shop by department", "4",
                    ("https://images.unsplash.com/photo-1511707171634-5f897ff02aa9?auto=format&fit=crop&w=800&q=80", "Phones", "Latest flagships"),
                    ("https://images.unsplash.com/photo-1496181133206-80ce9b88a853?auto=format&fit=crop&w=800&q=80", "Laptops", "Work + play"),
                    ("https://images.unsplash.com/photo-1505740420928-5e560c06d30e?auto=format&fit=crop&w=800&q=80", "Audio", "Immersive sound"),
                    ("https://images.unsplash.com/photo-1523275335684-37898b6baf30?auto=format&fit=crop&w=800&q=80", "Wearables", "Track everything")),
                Featured("Best sellers", "bestsellers", "carousel"),
                Multicolumn("Why shop with us",
                    ("🚚", "Free shipping", "On orders over ₹499"),
                    ("🛡️", "2-year warranty", "On all devices"),
                    ("↩️", "30-day returns", "No questions asked"),
                    ("⚡", "Fast support", "Real humans, 24/7")),
                Cta("Members save more", "Early access to launches and member-only deals.", "Become a member", "#0b1220"),
                Featured("New arrivals", "newest"),
            ], headingFont: "Space Grotesk", radius: "sharp", card: "elevated", density: "compact"),

        // Apparel (Avenue-inspired): lifestyle hero, For Her/Him/Kids persona tiles, editorial split, marquee.
        Theme("boutique", "Boutique", "Fashion", "Editorial apparel store — lifestyle hero, persona tiles and serif elegance.",
            palette: ("#d6336c", "#212529", "Poppins", "pill"),
            announce: "New season drops every week ✨",
            index:
            [
                HeroBanner("The new season edit", "Fresh silhouettes and timeless staples — made to be lived in.", "Shop new in",
                    "https://images.unsplash.com/photo-1445205170230-053b83016050?auto=format&fit=crop&w=1600&q=80"),
                TileGrid("Who are you shopping for?", "3",
                    ("https://images.unsplash.com/photo-1483985988355-763728e1935b?auto=format&fit=crop&w=800&q=80", "For Her", "Dresses, tops & more"),
                    ("https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?auto=format&fit=crop&w=800&q=80", "For Him", "Shirts, tees & denim"),
                    ("https://images.unsplash.com/photo-1519238263530-99bdd11df2ea?auto=format&fit=crop&w=800&q=80", "For Kids", "Play-proof styles")),
                Featured("New arrivals", "newest", "carousel"),
                Marquee("New drops every Friday ✦ Free returns, always", "#212529"),
                ImageWithText("Made to last", "Thoughtfully designed, ethically made. Quality you can feel in every stitch.",
                    "https://images.unsplash.com/photo-1558769132-cb1aea458c5e?auto=format&fit=crop&w=800&q=80"),
                Featured("Trending now", "bestsellers"),
                Cta("Join our list", "Get 10% off your first order.", "Sign up", "#212529"),
            ], headingFont: "Playfair Display", radius: "round", card: "shadow", density: "spacious",
               headerLayout: "centered", footerLayout: "simple"),

        // Food & drink (Savor-inspired): appetite-first photography, bold colour blocks, pantry tiles.
        Theme("savor", "Savor", "Food & Drink", "Appetite-first food and beverage store — rich photography and bold colour blocks.",
            palette: ("#b91c1c", "#7f1d1d", "Inter", "rounded"),
            announce: "Fresh batches every week · Free delivery over ₹499",
            index:
            [
                HeroBanner("Savor every last bite", "Small-batch flavours and pantry heroes, delivered fresh to your door.", "Shop the range",
                    "https://images.unsplash.com/photo-1504674900247-0877df9cc836?auto=format&fit=crop&w=1600&q=80"),
                TileGrid("Shop the pantry", "4",
                    ("https://images.unsplash.com/photo-1495474472287-4d71bcdd2085?auto=format&fit=crop&w=800&q=80", "Coffee & brews", ""),
                    ("https://images.unsplash.com/photo-1599490659213-e2b9527bd087?auto=format&fit=crop&w=800&q=80", "Snacks", ""),
                    ("https://images.unsplash.com/photo-1472476443507-c7a5948772fc?auto=format&fit=crop&w=800&q=80", "Sauces & spice", ""),
                    ("https://images.unsplash.com/photo-1610832958506-aa56368176cf?auto=format&fit=crop&w=800&q=80", "Fresh picks", "")),
                Featured("Customer favourites", "bestsellers", "carousel"),
                Marquee("Small batch ✦ Big flavour ✦ Made with love", "#7f1d1d"),
                Multicolumn("From our kitchen",
                    ("👨‍🍳", "Small-batch", "Made in real kitchens"),
                    ("🌿", "Real ingredients", "Nothing artificial"),
                    ("🚚", "Fresh delivery", "Over ₹499, on us"),
                    ("↩️", "Loved or refunded", "No questions asked")),
                Featured("Just landed", "newest"),
                Cta("Join the taste club", "First dibs on new flavours and 10% off your first box.", "Join now", "#7f1d1d"),
            ], headingFont: "Poppins", radius: "round", card: "shadow", density: "cozy"),

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
                TileGrid("Shop by ritual", "3",
                    ("https://images.unsplash.com/photo-1620916566398-39f1143ab7be?auto=format&fit=crop&w=800&q=80", "Serums", "Targeted actives"),
                    ("https://images.unsplash.com/photo-1556228720-195a672e8a03?auto=format&fit=crop&w=800&q=80", "Moisturisers", "Barrier love"),
                    ("https://images.unsplash.com/photo-1522335789203-aabd1fc54bc9?auto=format&fit=crop&w=800&q=80", "Makeup", "Everyday glow")),
                Featured("Bestsellers", "bestsellers", "carousel"),
                Multicolumn("The Bloom difference",
                    ("🌸", "Clean formulas", "Kind to skin"),
                    ("🐰", "Cruelty-free", "Always"),
                    ("🎁", "Free samples", "With every order"),
                    ("↩️", "Easy returns", "30-day promise")),
                ImageWithText("Kind to you and the planet", "Cruelty-free, dermatologically tested, thoughtfully packaged.",
                    "https://images.unsplash.com/photo-1556228720-195a672e8a03?auto=format&fit=crop&w=800&q=80"),
                Cta("Get 10% off", "Join for tips, launches and a welcome treat.", "Join Bloom", "#7c3aed"),
            ], headingFont: "Cormorant Garamond", radius: "round", card: "flat", density: "spacious",
               headerLayout: "centered", footerLayout: "simple"),

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

        // Kids & toys (Kidu-inspired): three-panel hero, marquee, rounded-everything playfulness.
        Theme("sprout", "Sprout", "Kids & Toys", "Playful, rounded kids store — three-panel hero, scrolling strip and joyful tiles.",
            palette: ("#7c3aed", "#4c1d95", "Nunito", "pill"),
            announce: "Free shipping over ₹499 · Gift wrap on every order 🎁",
            index:
            [
                HeroPanels("Explore the world of play", "Toys, books and little wardrobes — endless joy, zero boredom.", "Shop toys", "#7c3aed",
                    "https://images.unsplash.com/photo-1566576912321-d58ddd7a6088?auto=format&fit=crop&w=800&q=80",
                    "https://images.unsplash.com/photo-1515488042361-ee00e0ddd4e4?auto=format&fit=crop&w=800&q=80"),
                Marquee("New arrivals every week ✦ Made to be loved ✦ Gift wrap included", "#7c3aed"),
                TileGrid("Little favourites", "3",
                    ("https://images.unsplash.com/photo-1587654780291-39c9404d746b?auto=format&fit=crop&w=800&q=80", "Toys & games", "For every age"),
                    ("https://images.unsplash.com/photo-1519238263530-99bdd11df2ea?auto=format&fit=crop&w=800&q=80", "Kids' clothing", "Play-proof"),
                    ("https://images.unsplash.com/photo-1512076249812-fd58fb2c8748?auto=format&fit=crop&w=800&q=80", "Books & learning", "Curious minds")),
                Featured("Most loved", "bestsellers", "carousel"),
                Multicolumn("Grown-ups love us too",
                    ("🧸", "Safe materials", "Tested & certified"),
                    ("🎁", "Gift wrap", "Free on every order"),
                    ("🚚", "Fast delivery", "Before the birthday"),
                    ("↩️", "Easy returns", "30-day promise")),
                Featured("Just in", "newest"),
                Cta("Join the club", "Birthday surprises and early access to new drops.", "Sign up", "#4c1d95"),
            ], headingFont: "Nunito", radius: "round", card: "shadow", density: "cozy"),

        // Everything store (Maximize/xtra-inspired): dense, deals-first marketplace layout.
        Theme("bazaar", "Bazaar", "Everything Store", "Dense, deals-first marketplace look for stores that sell a bit of everything.",
            palette: ("#ea580c", "#0f172a", "Inter", "rounded"),
            announce: "Deals refresh daily · Free shipping over ₹499",
            index:
            [
                HeroBanner("Everything you need. One place.", "Thousands of products, daily deals and fast delivery.", "Shop today's deals",
                    "https://images.unsplash.com/photo-1534452203293-494d7ddbf7e0?auto=format&fit=crop&w=1600&q=80"),
                PromoTiles("Today's deals",
                    ("Up to 40% off", "Electronics", "https://images.unsplash.com/photo-1498049794561-7780e7231661?auto=format&fit=crop&w=800&q=80"),
                    ("From ₹199", "Fashion", "https://images.unsplash.com/photo-1441984904996-e0b6ba687e04?auto=format&fit=crop&w=800&q=80"),
                    ("Up to 30% off", "Home & living", "https://images.unsplash.com/photo-1616486338812-3dadae4b4ace?auto=format&fit=crop&w=800&q=80")),
                TileGrid("Shop by department", "3",
                    ("https://images.unsplash.com/photo-1542838132-92c53300491e?auto=format&fit=crop&w=800&q=80", "Grocery", "Everyday essentials"),
                    ("https://images.unsplash.com/photo-1596462502278-27bfdc403348?auto=format&fit=crop&w=800&q=80", "Beauty", "Skincare & more"),
                    ("https://images.unsplash.com/photo-1517649763962-0c623066013b?auto=format&fit=crop&w=800&q=80", "Sports & fitness", "Gear up")),
                Featured("Best sellers", "bestsellers", "carousel"),
                Multicolumn("Shop with confidence",
                    ("🚚", "Free shipping", "On orders over ₹499"),
                    ("💳", "Secure payments", "UPI, cards & COD"),
                    ("↩️", "Easy returns", "30-day, no fuss"),
                    ("💬", "Real support", "Humans, not bots")),
                Featured("Fresh finds", "newest"),
                Cta("Deals drop daily", "Don't miss tomorrow's steals — check back often.", "Browse all deals", "#0f172a"),
            ], headingFont: "Archivo", radius: "soft", card: "bordered", density: "compact"),
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
        string density = "cozy", string headingCase = "none",
        string headerLayout = "standard", string footerLayout = "columns") =>
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
                new("header", [Header(headerLayout)]),
                new("footer", [Footer($"© {name}. All rights reserved.", footerLayout)]),
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

    /// <summary>A three-panel hero: colour panel with copy flanked by two images (playful look).</summary>
    private static PrebuiltSection HeroPanels(string heading, string sub, string cta, string bg, string leftImage, string rightImage) => new(
        "Hero", "Hero", $$"""{"style":"panels","backgroundColor":{{J(bg)}},"autoplay":false}""",
        $$"""[{"image":"","heading":{{J(heading)}},"subheading":{{J(sub)}},"buttonText":{{J(cta)}},"buttonLink":"/products"},{"image":{{J(leftImage)}}},{"image":{{J(rightImage)}}}]""");

    /// <summary>A grid of image tiles with labels (categories/personas/lookbook).</summary>
    private static PrebuiltSection TileGrid(string heading, string columns, params (string image, string label, string sublabel)[] tiles)
    {
        var blocks = string.Join(",", tiles.Select(t =>
            $$"""{"image":{{J(t.image)}},"label":{{J(t.label)}},"sublabel":{{J(t.sublabel)}},"link":"/products"}"""));
        return new("TileGrid", "Image tiles", $$"""{"heading":{{J(heading)}},"columns":{{J(columns)}}}""", $"[{blocks}]");
    }

    /// <summary>Deal tiles with a badge + heading over an image or colour.</summary>
    private static PrebuiltSection PromoTiles(string heading, params (string badge, string headline, string image)[] tiles)
    {
        var blocks = string.Join(",", tiles.Select(t =>
            $$"""{"badge":{{J(t.badge)}},"heading":{{J(t.headline)}},"image":{{J(t.image)}},"link":"/products"}"""));
        return new("PromoTiles", "Promo tiles", $$"""{"heading":{{J(heading)}}}""", $"[{blocks}]");
    }

    /// <summary>A slim auto-scrolling text strip.</summary>
    private static PrebuiltSection Marquee(string text, string bg) => new(
        "Marquee", "Scrolling strip", $$"""{"text":{{J(text)}},"backgroundColor":{{J(bg)}}}""", "[]");

    private static PrebuiltSection Categories(string heading, string style = "grid") => new(
        "Categories", "Category strip", $$"""{"heading":{{J(heading)}},"style":{{J(style)}}}""", "[]");

    private static PrebuiltSection Featured(string heading, string source, string layout = "grid") => new(
        "FeaturedProducts", "Featured products", $$"""{"heading":{{J(heading)}},"layout":{{J(layout)}},"source":{{J(source)}},"count":8,"columns":4}""", "[]");

    private static PrebuiltSection ImageWithText(string heading, string body, string image = "") => new(
        "ImageWithText", "Image with text",
        $$"""{"image":{{J(image)}},"imageSide":"left","heading":{{J(heading)}},"body":{{J(body)}},"buttonText":"Learn more","buttonLink":"/products"}""", "[]");

    private static PrebuiltSection Testimonials() => new(
        "Testimonials", "Testimonials", """{"heading":"What customers say"}""",
        """[{"quote":"Great quality and quick delivery. Highly recommend!","author":"Happy customer","rating":5},{"quote":"Exactly as described. Will order again.","author":"Verified buyer","rating":5}]""");

    private static PrebuiltSection Cta(string heading, string sub, string cta, string bg) => new(
        "CtaNewsletter", "Call to action",
        $$"""{"heading":{{J(heading)}},"subtext":{{J(sub)}},"buttonText":{{J(cta)}},"buttonLink":"/products","backgroundColor":{{J(bg)}}}""", "[]");

    private static PrebuiltSection Announcement(string message, string bg) => new(
        "AnnouncementBar", "Announcement bar", $$"""{"backgroundColor":{{J(bg)}},"autoplay":true}""",
        $$"""[{"text":{{J(message)}},"link":"/products"}]""");

    private static PrebuiltSection Header(string layout = "standard") => new(
        "Header", "Header", $$"""{"layout":{{J(layout)}},"showSearch":true,"showCart":true,"sticky":true,"menuHandle":"main"}""", "[]");

    private static PrebuiltSection Footer(string copyright, string layout = "columns") => new(
        "Footer", "Footer", $$"""{"layout":{{J(layout)}},"showPolicies":true,"copyright":{{J(copyright)}}}""", "[]");

    private static string J(string s) => JsonSerializer.Serialize(s);
}
