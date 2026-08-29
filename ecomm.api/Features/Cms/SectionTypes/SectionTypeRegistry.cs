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
    IReadOnlyList<string>? Scope = null,
    /// <summary>App-marketplace S5: when set, this section type is provided by an app and is only offered
    /// to stores that have that app (by slug) installed. Null = core platform section.</summary>
    string? AppSlug = null);

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
        // App-provided block (S5) — only offered to stores with the "Trust Badges" app installed.
        new("TrustBadges", "Trust badges", "star",
            "A row of trust signals (secure payment, easy returns…). From the Trust Badges app.",
            Settings:
            [
                new("heading", "Heading", "text", ""),
                new("badge1", "Badge 1", "text", "🔒 Secure payments"),
                new("badge2", "Badge 2", "text", "↩️ Easy returns"),
                new("badge3", "Badge 3", "text", "✅ Genuine products"),
                new("badge4", "Badge 4", "text", "🚚 Fast delivery"),
            ], BlockTypes: [], AppSlug: "trust-badges"),
        new("ProductRecommendations", "Product recommendations", "grid",
            "A smart rail: Trending (demand velocity), Recommended (personalized), or Recently viewed. Self-hides until there's data.",
            Settings:
            [
                new("heading", "Heading", "text", "Recommended for you"),
                new("source", "Source", "select", "recommended", ["trending", "recommended", "recently-viewed"]),
                new("layout", "Layout", "select", "grid", ["grid", "carousel"]),
                new("count", "How many", "number", 8),
            ], BlockTypes: [], Kind: "dynamic"),
        new("TabbedProductGrid", "Tabbed product grid", "grid",
            "Tabs that switch the product grid in place, no page navigation — e.g. \"All / Earbuds / Smartwatches\".",
            Settings: [ new("heading", "Heading", "text") ],
            BlockTypes:
            [
                new("Tab", "Tab", [
                    new("label", "Tab label", "text"),
                    new("source", "Source", "select", "featured", ["featured", "newest", "bestsellers", "category"]),
                    new("categoryId", "Category (if source = category)", "category"),
                    new("count", "How many", "number", 8),
                ]),
            ], MaxBlocks: 6),

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
        new("ImageGallery", "Image gallery", "image", "A grid of photos that opens full-size in a lightbox when clicked — lookbooks, workshop/store photos, before-after shots.",
            Settings: [ new("heading", "Heading", "text") ],
            BlockTypes:
            [
                new("Photo", "Photo", [ new("image", "Image", "image"), new("caption", "Caption", "text") ]),
            ], MaxBlocks: 12),
        new("InstagramFeed", "Instagram-style feed", "grid",
            "A curated square photo grid styled like an Instagram feed, with a \"Follow us\" link to your real profile. Photos are uploaded here, not pulled live from Instagram.",
            Settings: [ new("heading", "Heading", "text", "Follow us"), new("handle", "Instagram handle", "text", Help: "Shown as @handle."), new("profileUrl", "Instagram profile URL", "url", Help: "Where the \"Follow us\" button links to.") ],
            BlockTypes:
            [
                new("Photo", "Photo", [ new("image", "Image", "image"), new("link", "Link (optional)", "url") ]),
            ], MaxBlocks: 8),
        new("CustomSection", "Custom section", "layout",
            "A blank canvas — freely mix headings, text, images, buttons, spacers and dividers in any order to build a one-off layout no other section covers.",
            Settings: [],
            BlockTypes:
            [
                new("Heading", "Heading", [ new("text", "Text", "text"), new("size", "Size", "select", "Medium", ["Large", "Medium", "Small"]) ]),
                new("Text", "Text", [ new("content", "Content", "richtext") ]),
                new("Image", "Image", [ new("image", "Image", "image"), new("link", "Link (optional)", "url") ]),
                new("Button", "Button", [ new("text", "Text", "text"), new("link", "Link", "link"), new("style", "Style", "select", "Primary", ["Primary", "Secondary"]) ]),
                new("Spacer", "Spacer", [ new("height", "Height", "select", "Medium", ["Small", "Medium", "Large"]) ]),
                new("Divider", "Divider", []),
            ], MaxBlocks: 20),

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
        new("FrequentlyBoughtTogether", "Frequently bought together", "grid",
            "A bundle of products often purchased alongside this one, with a combined add-to-cart. Based on real order history.",
            Settings: [ new("heading", "Heading", "text", "Frequently bought together"), new("count", "How many", "number", 3) ],
            BlockTypes: [], Kind: "dynamic", Scope: ["product"]),
        new("RecentlyViewed", "Recently viewed", "grid", "A rail of products the shopper has recently looked at (tracked in their browser).",
            Settings: [ new("heading", "Heading", "text", "Recently viewed"), new("count", "How many", "number", 8) ],
            BlockTypes: [], Kind: "dynamic", Scope: null),
        new("CollectionHeader", "Collection header", "layout", "Collection title, description and image.",
            Settings:
            [
                new("bannerImage", "Banner image (optional)", "image"),
                new("overlayColor", "Banner overlay color", "color"),
                new("textAlign", "Text alignment", "select", "left", ["left", "center"]),
                new("description", "Description override (optional)", "textarea"),
            ],
            BlockTypes: [], Kind: "dynamic", Scope: ["collection"]),
        new("CollectionCategories", "Collection categories", "tag",
            "Shop-by-category tiles. Shown only on the \"All products\" view (hidden once a specific category is active).",
            Settings:
            [
                new("heading", "Heading", "text", "Shop by category"),
                new("style", "Style", "select", "cards", ["cards", "icons"]),
                new("columns", "Columns", "number", 6),
            ],
            BlockTypes: [], Kind: "dynamic", Scope: ["collection"]),
        new("CollectionGrid", "Collection grid", "grid", "The product grid with filters and sort.",
            Settings:
            [
                new("columnsDesktop", "Columns (desktop)", "number", 4),
                new("columnsMobile", "Columns (mobile)", "number", 2),
                new("showFilters", "Show filters", "boolean", true),
                new("showSort", "Show sort", "boolean", true),
                new("showCategorySidebar", "Show category sidebar", "boolean", true),
                new("productsPerPage", "Products per page", "number", 12),
                new("cardAspect", "Product image shape", "select", "square", ["square", "portrait"]),
                new("paginationStyle", "Pagination style", "select", "numbered", ["numbered", "loadMore"]),
            ],
            BlockTypes: [], Kind: "dynamic", Scope: ["collection"]),
        new("CollectionsList", "Collections list", "grid", "A grid of all collections.",
            Settings: [ new("heading", "Heading", "text", "Collections"), new("columns", "Columns", "number", 3) ],
            BlockTypes: [], Kind: "dynamic", Scope: ["list-collections"]),
        new("CartItems", "Cart items", "cart", "The line items in the cart.",
            Settings: [], BlockTypes: [], Kind: "dynamic", Scope: ["cart"]),
        new("CartSummary", "Cart summary", "tag", "Totals, checkout button, coupon field, shipping estimator and order notes.",
            Settings:
            [
                new("showCouponField", "Show coupon field", "boolean", true),
                new("showShippingEstimator", "Show delivery pincode estimator", "boolean", true),
                new("showOrderNotes", "Show \"Add a note\"", "boolean", true),
            ],
            BlockTypes: [], Kind: "dynamic", Scope: ["cart"]),
        new("CartCrossSell", "Cart cross-sell", "grid", "A product rail (\"You might also like\") shown alongside the cart.",
            Settings:
            [
                new("heading", "Heading", "text", "You might also like"),
                new("source", "Source", "select", "bestsellers", ["bestsellers", "featured", "newest"]),
                new("count", "How many", "number", 4),
            ],
            BlockTypes: [], Kind: "dynamic", Scope: ["cart"]),
        new("SearchBar", "Search bar", "search", "The storefront search input.",
            Settings: [ new("placeholder", "Placeholder text", "text", "Search products…") ],
            BlockTypes: [], Kind: "dynamic", Scope: ["search"]),
        new("SearchResults", "Search results", "grid", "Results for the current query — same filter/sort/grid as the collection page.",
            Settings:
            [
                new("columnsDesktop", "Columns (desktop)", "number", 4),
                new("columnsMobile", "Columns (mobile)", "number", 2),
                new("showFilters", "Show filters", "boolean", true),
                new("showSort", "Show sort", "boolean", true),
                new("showCategorySidebar", "Show category sidebar", "boolean", true),
                new("productsPerPage", "Products per page", "number", 12),
                new("cardAspect", "Card aspect", "select", "square", ["square", "portrait"]),
                new("paginationStyle", "Pagination style", "select", "numbered", ["numbered", "loadMore"]),
            ],
            BlockTypes: [], Kind: "dynamic", Scope: ["search"]),
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
