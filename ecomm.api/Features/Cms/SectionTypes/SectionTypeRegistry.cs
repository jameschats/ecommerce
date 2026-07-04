namespace ecomm.api.Features.Cms.SectionTypes;

/// <summary>A settings/block field. Type drives the builder input + validation.</summary>
public sealed record FieldSchema(
    string Key, string Label, string Type, object? Default = null, string[]? Options = null, string? Help = null);
// Type: text | textarea | richtext | number | boolean | color | image | url | select | category

/// <summary>A block kind allowed inside a section (e.g. a hero Slide, a Testimonial item).</summary>
public sealed record BlockTypeSchema(string Key, string Label, IReadOnlyList<FieldSchema> Fields);

/// <summary>A section type: its settings schema + which block kinds it may contain.</summary>
public sealed record SectionTypeSchema(
    string Key, string Label, string Icon, string? Description,
    IReadOnlyList<FieldSchema> Settings,
    IReadOnlyList<BlockTypeSchema> BlockTypes,
    int? MaxBlocks = null);

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
                new("autoplay", "Auto-play slides", "boolean", true),
                new("intervalSec", "Seconds per slide", "number", 5),
            ],
            BlockTypes:
            [
                new("Slide", "Slide",
                [
                    new("image", "Image", "image"),
                    new("heading", "Heading", "text"),
                    new("subheading", "Subheading", "textarea"),
                    new("buttonText", "Button text", "text"),
                    new("buttonLink", "Button link", "url"),
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
                new("source", "Source", "select", "featured", ["featured", "newest", "bestsellers", "category"]),
                new("categoryId", "Category (if source = category)", "category"),
                new("count", "How many", "number", 8),
                new("columns", "Columns", "number", 4),
            ], BlockTypes: []),

        new("Categories", "Category strip", "tag", "Shop-by-category tiles.",
            Settings:
            [
                new("heading", "Heading", "text", "Shop by category"),
                new("style", "Style", "select", "grid", ["grid", "strip"]),
            ], BlockTypes: []),

        new("ImageWithText", "Image with text", "image", "An image beside a heading + copy + button.",
            Settings:
            [
                new("image", "Image", "image"),
                new("imageSide", "Image side", "select", "left", ["left", "right"]),
                new("heading", "Heading", "text"),
                new("body", "Body", "textarea"),
                new("buttonText", "Button text", "text"),
                new("buttonLink", "Button link", "url"),
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
                new("buttonLink", "Button link", "url"),
                new("backgroundColor", "Background colour", "color", "#111827"),
            ], BlockTypes: []),
    };

    public static SectionTypeSchema? Get(string key) =>
        All.FirstOrDefault(s => string.Equals(s.Key, key, StringComparison.OrdinalIgnoreCase));

    public static bool IsValidType(string key) => Get(key) is not null;

    /// <summary>Keys of settings fields that hold HTML and must be sanitized on save.</summary>
    public static IEnumerable<string> RichTextSettingKeys(string sectionType) =>
        Get(sectionType)?.Settings.Where(f => f.Type == "richtext").Select(f => f.Key) ?? [];
}
