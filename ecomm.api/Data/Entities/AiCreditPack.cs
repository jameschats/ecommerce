namespace ecomm.api.Data.Entities;

/// <summary>A buyable AI credit top-up pack. Platform-priced and global (not tenant-scoped).</summary>
public class AiCreditPack
{
    public int AiCreditPackId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Credits { get; set; }
    public decimal PriceInr { get; set; }
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
