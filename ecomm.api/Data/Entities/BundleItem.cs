namespace ecomm.api.Data.Entities;

/// <summary>One component of a bundle Product (<see cref="Data.Entities.Product.IsBundle"/>) — the real
/// product+variant+quantity that gets reserved/committed/released from inventory when the bundle sells.
/// The bundle itself holds no inventory of its own.</summary>
public class BundleItem : ITenantScoped
{
    public long BundleItemId { get; set; }
    public long TenantId { get; set; } = 1;
    public long BundleProductId { get; set; }
    public long ComponentProductId { get; set; }
    public long? ComponentVariantId { get; set; }
    public int Quantity { get; set; } = 1;
}
