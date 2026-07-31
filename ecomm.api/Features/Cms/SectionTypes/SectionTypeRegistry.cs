namespace ecomm.api.Features.Cms.SectionTypes;

/// <summary>A settings/block field. Type drives the builder input + validation.</summary>
public sealed record FieldSchema(
    string Key, string Label, string Type, object? Default = null, string[]? Options = null, string? Help = null,
    decimal? Min = null, decimal? Max = null, decimal? Step = null);
// Type: text | textarea | richtext | number | boolean | color | image | url | select | category
//     | product | collection | page | menu | link | range (Min/Max/Step apply to range only)
//     | colorScheme (a theme-defined named palette — see Theme.Settings["ColorSchemes"])
//     | datetime (stored as a UTC ISO 8601 string; editor converts to/from the visitor's local time)

/// <summary>A block kind allowed inside a section (e.g. a hero Slide, a Testimonial item).</summary>
public sealed record BlockTypeSchema(string Key, string Label, IReadOnlyList<FieldSchema> Fields);

/// <summary>
/// A section type: its settings schema + which block kinds it may contain.
/// <para><c>Kind</c>: <c>static</c> (settings only) · <c>dynamic</c> (binds live route data:
/// product/collection/cart/search) · <c>group</c> (shared header/footer/announcement zone).</para>
/// <para><c>Scope</c>: template keys this section is valid on; <c>null</c> = any template.</para>
/// </summary>
public sealed record SectionTypeSchema(
    string Key, string Label, string Icon, string? Description,
    IReadOnlyList<FieldSchema> Settings,
    IReadOnlyList<BlockTypeSchema> BlockTypes,
    int? MaxBlocks = null,
    string Kind = "static",
    IReadOnlyList<string>? Scope = null);

/// <summary>
/// The platform's catalog of storefront section types. This is the single source of
/// truth the builder renders settings forms from and the backend validates against.
/// New section types are added HERE (centrally) — merchants never write code.
/// </summary>
public static class SectionTypeRegistry
{
    public static readonly IReadOnlyList<SectionTypeSchema> All = new List<SectionTypeSchema>
    {
        new("Hero", "Hero / Slideshow", "image", "A big banner or auto-playing slideshow.",
            Settings:
            [
                new("style", "Layout", "select", "boxed", ["boxed", "split", "banner", "panels", "carousel"]),
                new("backgroundColor", "Background colour (panels)", "color", Help: "Centre-panel colour for the panels layout."),
                new("colorScheme", "Colour scheme (panels)", "colorScheme", Help: "Overrides the background colour above, if set."),
                new("autoplay", "Auto-play slides", "boolean", true),
                new("intervalSec", "Seconds per slide", "range", 5, Min: 2, Max: 10, Step: 1),
            ],
            BlockTypes:
            [
                new("Slide", "Slide",
                [
                    new("image", "Image", "image"),
                    new("heading", "Heading", "text"),
                    new("subheading", "Subheading", "textarea"),
                    new("buttonText", "Button text", "text"),
                    new("buttonLink", "Button link", "link"),
                ]),
            ], MaxBlocks: 8),

        new("RichText", "Rich text", "text", "Free-form formatted text (sanitized — no scripts).",
            Settings:
            [
                new("content", "Content", "richtext"),
                new("align", "Alignment", "select", "left", ["left", "center", "right"]),
            ], BlockTypes: []),

        new("FeaturedProducts", "Featured products", "grid", "A rail of products.",
            Settings:
            [
                new("heading", "Heading", "text", "Featured"),
                new("layout", "Layout", "select", "grid", ["grid", "carousel"]),
                new("source", "Source", "select", "featured", ["featured", "newest", "bestsellers", "category", "collection"]),
                new("categoryId", "Category (if source = category)", "category"),
                new("collectionId", "Collection (if source = collection)", "collection"),
                new("count", "How many", "number", 8),
                new("columns", "Columns", "range", 4, Min: 1, Max: 6, Step: 1),
            ], BlockTypes: []),

        new("Multicolumn", "Feature columns", "grid", "A row of icon + heading + text tiles (USPs, how-it-works).",
            Settings: [ new("heading", "Heading", "text") ],
            BlockTypes:
            [
                new("Column", "Column",
                [
                    new("icon", "Icon (emoji)", "text"),
                    new("heading", "Heading", "text"),
                    new("text", "Text", "textarea"),
                ]),
            ], MaxBlocks: 6),

        new("Categories", "Category strip", "tag", "Shop-by-category tiles.",
            Settings:
            [
                new("heading", "Heading", "text", "Shop by category"),
                new("style", "Style", "select", "grid", ["grid", "cards", "strip"]),
            ], BlockTypes: []),

        new("ImageWithText", "Image with text", "image", "An image beside a heading + copy + button.",
            Settings:
            [
                new("image", "Image", "image"),
                new("imageSide", "Image side", "select", "left", ["left", "right"]),
                new("heading", "Heading", "text"),
                new("body", "Body", "textarea"),
                new("buttonText", "Button text", "text"),
                new("buttonLink", "Button link", "link"),
            ], BlockTypes: []),

        new("Testimonials", "Testimonials", "star", "Customer quotes.",
            Settings: [ new("heading", "Heading", "text", "What customers say") ],
            BlockTypes:
            [
                new("Testimonial", "Testimonial",
                [
                    new("quote", "Quote", "textarea"),
                    new("author", "Author", "text"),
                    new("rating", "Rating (1-5)", "number", 5),
                ]),
            ], MaxBlocks: 12),

        new("CtaNewsletter", "Call to action", "megaphone", "A promo band with a button.",
            Settings:
            [
                new("heading", "Heading", "text"),
                new("subtext", "Subtext", "textarea"),
                new("buttonText", "Button text", "text"),
                new("buttonLink", "Button link", "link"),
                new("backgroundColor", "Background colour", "color", "#111827"),
                new("colorScheme", "Colour scheme", "colorScheme", Help: "Overrides the background colour above, if set."),
            ], BlockTypes: []),

        new("TileGrid", "Image tiles", "grid", "A grid of image tiles with labels — categories, personas or lookbook shots.",
            Settings:
            [
                new("heading", "Heading", "text"),
                new("columns", "Columns", "select", "3", ["2", "3", "4"]),
            ],
            BlockTypes:
            [
                new("Tile", "Tile", [ new("image", "Image", "image"), new("label", "Label", "text"), new("sublabel", "Sub-label", "text"), new("link", "Link", "link") ]),
            ], MaxBlocks: 6),

        new("PromoTiles", "Promo tiles", "tag", "Deal tiles with a badge and heading over an image or colour.",
            Settings: [ new("heading", "Heading", "text") ],
            BlockTypes:
            [
                new("Tile", "Promo", [ new("badge", "Badge", "text"), new("heading", "Heading", "text"), new("text", "Text", "text"), new("image", "Image", "image"), new("backgroundColor", "Background colour", "color"), new("colorScheme", "Colour scheme", "colorScheme"), new("link", "Link", "link") ]),
            ], MaxBlocks: 6),

        new("Marquee", "Scrolling strip", "megaphone", "A slim auto-scrolling text strip for offers or brand personality.",
            Settings:
            [
                new("text", "Text", "text", "Free shipping over ₹499"),
                new("backgroundColor", "Background colour", "color", "#111827"),
                new("colorScheme", "Colour scheme", "colorScheme", Help: "Overrides the background colour above, if set."),
            ], BlockTypes: []),

        new("CountdownBar", "Countdown / promo bar", "megaphone", "A slim bar with a live countdown to a sale's end.",
            Settings:
            [
                new("heading", "Heading", "text", "Sale ends in:"),
                new("endDateTime", "Ends at", "datetime"),
                new("expiredText", "Message after it ends", "text", "This offer has ended"),
                new("buttonText", "Button text", "text"),
                new("buttonLink", "Button link", "link"),
                new("backgroundColor", "Background colour", "color", "#111827"),
                new("colorScheme", "Colour scheme", "colorScheme", Help: "Overrides the background colour above, if set."),
            ], BlockTypes: []),

        new("Collage", "Image collage", "grid", "An asymmetric mosaic of lifestyle/editorial photos — one large tile plus smaller ones.",
            Settings: [ new("heading", "Heading", "text") ],
            BlockTypes:
            [
                new("Tile", "Tile", [ new("image", "Image", "image"), new("link", "Link", "link"), new("span", "Size", "select", "small", ["large", "small"]) ]),
            ], MaxBlocks: 5),

        new("EditorialSplit", "Editorial split", "image", "A big magazine-style image beside a large heading and body copy — brand story, about-us feel.",
            Settings:
            [
                new("eyebrow", "Eyebrow label", "text", Help: "Small label above the heading, e.g. \"Since 2015\"."),
                new("heading", "Heading", "text"),
                new("body", "Body", "textarea"),
                new("image", "Image", "image"),
                new("imageSide", "Image side", "select", "left", ["left", "right"]),
                new("buttonText", "Button text", "text"),
                new("buttonLink", "Button link", "link"),
            ], BlockTypes: []),

        new("FaqAccordion", "FAQ accordion", "info", "Collapsible questions and answers.",
            Settings: [ new("heading", "Heading", "text", "Frequently asked questions") ],
            BlockTypes:
            [
                new("Item", "Question", [ new("question", "Question", "text"), new("answer", "Answer", "textarea") ]),
            ], MaxBlocks: 10),

        new("VideoSection", "Video", "image", "An embedded YouTube/Vimeo video or an MP4 file, with an optional heading and caption.",
            Settings:
            [
                new("heading", "Heading", "text"),
                new("videoUrl", "Video URL", "url", Help: "A YouTube or Vimeo link, or a direct .mp4 file URL."),
                new("posterImage", "Poster image", "image", Help: "Shown before the video loads, and as the background for the MP4 case."),
                new("caption", "Caption", "text"),
            ], BlockTypes: []),

        new("LogoStrip", "Logo strip", "grid", "A row of partner/press/brand logos — \"as seen in\", \"trusted by\".",
            Settings: [ new("heading", "Heading", "text") ],
            BlockTypes:
            [
                new("Logo", "Logo", [ new("image", "Image", "image"), new("label", "Label (alt text)", "text"), new("link", "Link", "link") ]),
            ], MaxBlocks: 8),

        new("Stats", "Stats bar", "tag", "Big-number trust callouts — years in business, products shipped, happy customers.",
            Settings: [ new("heading", "Heading", "text") ],
            BlockTypes:
            [
                new("Stat", "Stat", [ new("value", "Value", "text"), new("label", "Label", "text") ]),
            ], MaxBlocks: 4),

        // ---- Group sections (shared zones, one per theme) ----
        new("AnnouncementBar", "Announcement bar", "megaphone", "A thin bar above the header for promos/notices.",
            Settings:
            [
                new("backgroundColor", "Background colour", "color", "#111827"),
                new("colorScheme", "Colour scheme", "colorScheme", Help: "Overrides the background colour above, if set."),
                new("autoplay", "Rotate messages", "boolean", true),
            ],
            BlockTypes: [ new("Message", "Message", [ new("text", "Text", "text"), new("link", "Link", "link") ]) ],
            MaxBlocks: 5, Kind: "group", Scope: ["announcement"]),

        new("Header", "Header", "layout", "Logo, navigation, search and cart.",
            Settings:
            [
                new("layout", "Layout", "select", "standard", ["standard", "centered", "minimal"]),
                new("showSearch", "Show search", "boolean", true),
                new("showCart", "Show cart", "boolean", true),
                new("sticky", "Stick to top on scroll", "boolean", true),
                new("menuHandle", "Menu", "menu", "main"),
            ], BlockTypes: [], Kind: "group", Scope: ["header"]),

        new("Footer", "Footer", "layout", "Link columns, socials and legal.",
            Settings:
            [
                new("layout", "Layout", "select", "columns", ["columns", "simple"]),
                new("showPolicies", "Show policy links", "boolean", true),
                new("copyright", "Copyright text", "text"),
            ],
            BlockTypes:
            [
                new("Column", "Link column", [ new("heading", "Heading", "text"), new("links", "Links (JSON)", "textarea") ]),
            ], MaxBlocks: 5, Kind: "group", Scope: ["footer"]),

        // ---- Dynamic sections (bind live route data; valid only on their page-type) ----
        new("Breadcrumbs", "Breadcrumbs", "chevron", "The trail to the current page.",
            Settings: [], BlockTypes: [], Kind: "dynamic", Scope: ["product", "collection"]),
        new("ProductGallery", "Product gallery", "image", "The product's image gallery.",
            Settings: [ new("zoom", "Enable zoom", "boolean", true) ], BlockTypes: [], Kind: "dynamic", Scope: ["product"]),
        new("ProductInfo", "Product info", "tag", "Title, price, variants, quantity and add-to-cart.",
            Settings: [ new("showSku", "Show SKU", "boolean", true), new("showShare", "Show share buttons", "boolean", false) ],
            BlockTypes: [], Kind: "dynamic", Scope: ["product"]),
        new("ProductDescription", "Product description", "text", "The full product description.",
            Settings: [], BlockTypes: [], Kind: "dynamic", Scope: ["product"]),
        new("ProductReviews", "Product reviews", "star", "Ratings and customer reviews.",
            Settings: [], BlockTypes: [], Kind: "dynamic", Scope: ["product"]),
        new("RelatedProducts", "Related products", "grid", "A rail of related products.",
            Settings: [ new("heading", "Heading", "text", "You may also like"), new("count", "How many", "number", 8) ],
            BlockTypes: [], Kind: "dynamic", Scope: ["product"]),
        new("CollectionHeader", "Collection header", "layout", "Collection title, description and image.",
            Settings: [], BlockTypes: [], Kind: "dynamic", Scope: ["collection"]),
        new("CollectionGrid", "Collection grid", "grid", "The product grid with filters and sort.",
            Settings: [ new("columns", "Columns", "number", 4), new("showFilters", "Show filters", "boolean", true), new("showSort", "Show sort", "boolean", true) ],
            BlockTypes: [], Kind: "dynamic", Scope: ["collection"]),
        new("CollectionsList", "Collections list", "grid", "A grid of all collections.",
            Settings: [ new("columns", "Columns", "number", 3) ], BlockTypes: [], Kind: "dynamic", Scope: ["list-collections"]),
        new("CartItems", "Cart items", "cart", "The line items in the cart.",
            Settings: [], BlockTypes: [], Kind: "dynamic", Scope: ["cart"]),
        new("CartSummary", "Cart summary", "tag", "Totals and checkout button.",
            Settings: [], BlockTypes: [], Kind: "dynamic", Scope: ["cart"]),
        new("SearchBar", "Search bar", "search", "The storefront search input.",
            Settings: [], BlockTypes: [], Kind: "dynamic", Scope: ["search"]),
        new("SearchResults", "Search results", "grid", "Results for the current query.",
            Settings: [ new("columns", "Columns", "number", 4) ], BlockTypes: [], Kind: "dynamic", Scope: ["search"]),
        new("EmptyState", "Empty state", "info", "Shown when there's nothing to display (404 / empty cart / no results).",
            Settings: [ new("heading", "Heading", "text"), new("body", "Body", "textarea"), new("buttonText", "Button text", "text"), new("buttonLink", "Button link", "link") ],
            BlockTypes: [], Kind: "dynamic", Scope: ["404", "cart", "search"]),
    };

    /// <summary>Known template (page-type) keys a theme can define.</summary>
    public static readonly IReadOnlyList<string> TemplateKeys =
    [
        "index", "product", "collection", "list-collections", "cart", "search", "404", "password", "account",
        "header", "footer", "announcement",
    ];

    /// <summary>Shared-zone templates — they accept ONLY their own group section, never any-scope statics.</summary>
    private static readonly HashSet<string> GroupZones = new(StringComparer.OrdinalIgnoreCase) { "header", "footer", "announcement" };

    public static SectionTypeSchema? Get(string key) =>
        All.FirstOrDefault(s => string.Equals(s.Key, key, StringComparison.OrdinalIgnoreCase));

    public static bool IsValidType(string key) => Get(key) is not null;

    public static bool IsValidTemplateKey(string key) =>
        TemplateKeys.Any(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase));

    /// <summary>True if the section type may be placed on the given template (respecting its Scope).</summary>
    public static bool IsValidOnTemplate(string sectionType, string templateKey)
    {
        var schema = Get(sectionType);
        return schema is not null && ScopeAllows(schema, templateKey);
    }

    /// <summary>Section types valid on a template (for the builder's "add section" list).</summary>
    public static IEnumerable<SectionTypeSchema> ForTemplate(string templateKey) =>
        All.Where(s => ScopeAllows(s, templateKey));

    /// <summary>
    /// A section is allowed on a template when its Scope names that template. An open (null) Scope
    /// means "any page template" — but NOT the shared Header/Footer/Announcement zones, which only
    /// accept sections explicitly scoped to them (so the editor never offers a Hero on the header).
    /// </summary>
    private static bool ScopeAllows(SectionTypeSchema schema, string templateKey)
    {
        if (schema.Scope is not null)
            return schema.Scope.Any(s => string.Equals(s, templateKey, StringComparison.OrdinalIgnoreCase));
        return !GroupZones.Contains(templateKey);
    }

    /// <summary>Keys of settings fields that hold HTML and must be sanitized on save.</summary>
    public static IEnumerable<string> RichTextSettingKeys(string sectionType) =>
        Get(sectionType)?.Settings.Where(f => f.Type == "richtext").Select(f => f.Key) ?? [];
}
