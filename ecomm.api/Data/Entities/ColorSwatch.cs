namespace ecomm.api.Data.Entities;

/// <summary>A merchant-editable colour name -&gt; hex mapping used to render swatch dots for
/// free-text <see cref="VariantOption"/> colour values (migration 255).</summary>
public class ColorSwatch : ITenantScoped
{
    public long ColorSwatchId { get; set; }
    public long TenantId { get; set; } = 1;
    public string Name { get; set; } = string.Empty;
    public string HexCode { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
