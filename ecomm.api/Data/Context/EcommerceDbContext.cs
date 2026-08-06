using ecomm.api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Data.Context;

/// <summary>
/// Database-first context: the SQL migrations in /database/migrations are the
/// source of truth. Entities are hand-mapped to existing tables here (we do NOT
/// generate EF migrations). DbSets/entities grow per feature slice.
/// </summary>
public class EcommerceDbContext : DbContext
{
    public EcommerceDbContext(DbContextOptions<EcommerceDbContext> options) : base(options) { }

    // --- Core / Identity ---
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    // --- Auth ---
    public DbSet<AuthProvider> AuthProviders => Set<AuthProvider>();
    public DbSet<UserExternalLogin> UserExternalLogins => Set<UserExternalLogin>();
    public DbSet<OtpVerification> OtpVerifications => Set<OtpVerification>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    // --- Catalog ---
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<VariantOption> VariantOptions => Set<VariantOption>();
    public DbSet<AttributeDefinition> Attributes => Set<AttributeDefinition>();
    public DbSet<AttributeValue> AttributeValues => Set<AttributeValue>();
    public DbSet<ProductAttributeValue> ProductAttributeValues => Set<ProductAttributeValue>();

    // --- Import jobs ---
    public DbSet<ImportJob> ImportJobs => Set<ImportJob>();
    public DbSet<ImportJobItem> ImportJobItems => Set<ImportJobItem>();

    // --- Theme ---
    public DbSet<Theme> Themes => Set<Theme>();
    public DbSet<ThemeSetting> ThemeSettings => Set<ThemeSetting>();

    // --- CMS ---
    public DbSet<Page> Pages => Set<Page>();
    public DbSet<PageSection> PageSections => Set<PageSection>();

    // --- Inventory & Search ---
    public DbSet<Inventory> Inventory => Set<Inventory>();
    public DbSet<InventoryTransaction> InventoryTransactions => Set<InventoryTransaction>();
    public DbSet<SearchLog> SearchLogs => Set<SearchLog>();
    public DbSet<PopularSearch> PopularSearches => Set<PopularSearch>();

    // --- Shopping ---
    public DbSet<CustomerAddress> CustomerAddresses => Set<CustomerAddress>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();

    // --- Checkout / Orders / Billing ---
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OrderStatusHistory> OrderStatusHistories => Set<OrderStatusHistory>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();
    public DbSet<Refund> Refunds => Set<Refund>();
    public DbSet<TaxRate> TaxRates => Set<TaxRate>();
    public DbSet<ShippingMethod> ShippingMethods => Set<ShippingMethod>();
    public DbSet<ShippingZone> ShippingZones => Set<ShippingZone>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();
    public DbSet<Setting> Settings => Set<Setting>();
    public DbSet<Contact> Contacts => Set<Contact>();
    public DbSet<Campaign> Campaigns => Set<Campaign>();
    public DbSet<CampaignRecipient> CampaignRecipients => Set<CampaignRecipient>();
    public DbSet<StateMinOrderAmount> StateMinOrderAmounts => Set<StateMinOrderAmount>();
    public DbSet<HomeBanner> HomeBanners => Set<HomeBanner>();
    public DbSet<GalleryImage> GalleryImages => Set<GalleryImage>();
    public DbSet<MediaFile> MediaFiles => Set<MediaFile>();
    public DbSet<NotificationTemplate> NotificationTemplates => Set<NotificationTemplate>();
    public DbSet<NotificationHistory> NotificationHistory => Set<NotificationHistory>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Coupon> Coupons => Set<Coupon>();
    public DbSet<CouponUsage> CouponUsages => Set<CouponUsage>();
    public DbSet<Shipment> Shipments => Set<Shipment>();
    public DbSet<WishlistItem> WishlistItems => Set<WishlistItem>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<ProductSupplier> ProductSuppliers => Set<ProductSupplier>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Tenant>(e => { e.ToTable("Tenants"); e.HasKey(x => x.TenantId); });

        b.Entity<User>(e =>
        {
            e.ToTable("Users");
            e.HasKey(x => x.UserId);
        });

        b.Entity<Role>(e => { e.ToTable("Roles"); e.HasKey(x => x.RoleId); });

        b.Entity<Permission>(e => { e.ToTable("Permissions"); e.HasKey(x => x.PermissionId); });

        b.Entity<UserRole>(e =>
        {
            e.ToTable("UserRoles");
            e.HasKey(x => new { x.UserId, x.RoleId });
            e.HasOne(x => x.User).WithMany(u => u.UserRoles).HasForeignKey(x => x.UserId);
            e.HasOne(x => x.Role).WithMany(r => r.UserRoles).HasForeignKey(x => x.RoleId);
        });

        b.Entity<RolePermission>(e =>
        {
            e.ToTable("RolePermissions");
            e.HasKey(x => new { x.RoleId, x.PermissionId });
            e.HasOne(x => x.Role).WithMany(r => r.RolePermissions).HasForeignKey(x => x.RoleId);
            e.HasOne(x => x.Permission).WithMany(p => p.RolePermissions).HasForeignKey(x => x.PermissionId);
        });

        b.Entity<AuthProvider>(e => { e.ToTable("AuthProviders"); e.HasKey(x => x.AuthProviderId); });

        b.Entity<UserExternalLogin>(e =>
        {
            e.ToTable("UserExternalLogins");
            e.HasKey(x => x.UserExternalLoginId);
            e.HasOne(x => x.User).WithMany(u => u.ExternalLogins).HasForeignKey(x => x.UserId);
        });

        b.Entity<OtpVerification>(e => { e.ToTable("OtpVerifications"); e.HasKey(x => x.OtpVerificationId); });

        b.Entity<RefreshToken>(e =>
        {
            e.ToTable("RefreshTokens");
            e.HasKey(x => x.RefreshTokenId);
            e.Ignore(x => x.IsActive);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
        });

        // --- Catalog ---
        b.Entity<Category>(e =>
        {
            e.ToTable("Categories");
            e.HasKey(x => x.CategoryId);
            e.HasOne(x => x.Parent).WithMany(c => c.Children).HasForeignKey(x => x.ParentCategoryId);
        });

        b.Entity<Brand>(e => { e.ToTable("Brands"); e.HasKey(x => x.BrandId); });

        b.Entity<Product>(e =>
        {
            e.ToTable("Products");
            e.HasKey(x => x.ProductId);
            e.Property(x => x.Price).HasPrecision(12, 2);
            e.Property(x => x.CompareAtPrice).HasPrecision(12, 2);
            e.Property(x => x.CostPrice).HasPrecision(12, 2);
            e.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId);
            e.HasOne(x => x.Brand).WithMany().HasForeignKey(x => x.BrandId);
            e.HasMany(x => x.Images).WithOne(i => i.Product!).HasForeignKey(i => i.ProductId);
        });

        b.Entity<ProductImage>(e => { e.ToTable("ProductImages"); e.HasKey(x => x.ProductImageId); });

        b.Entity<ProductVariant>(e =>
        {
            e.ToTable("ProductVariants");
            e.HasKey(x => x.ProductVariantId);
            e.Property(x => x.PriceAdjustment).HasPrecision(12, 2);
            e.HasMany(x => x.Options).WithOne(o => o.Variant!).HasForeignKey(o => o.ProductVariantId);
        });

        b.Entity<VariantOption>(e => { e.ToTable("VariantOptions"); e.HasKey(x => x.VariantOptionId); });

        b.Entity<AttributeDefinition>(e =>
        {
            e.ToTable("Attributes");
            e.HasKey(x => x.AttributeId);
            e.HasMany(x => x.Values).WithOne(v => v.Attribute!).HasForeignKey(v => v.AttributeId);
        });

        b.Entity<AttributeValue>(e => { e.ToTable("AttributeValues"); e.HasKey(x => x.AttributeValueId); });

        b.Entity<ProductAttributeValue>(e =>
        {
            e.ToTable("ProductAttributeValues");
            e.HasKey(x => x.ProductAttributeValueId);
            e.HasOne(x => x.Attribute).WithMany().HasForeignKey(x => x.AttributeId);
            e.HasOne(x => x.Value).WithMany().HasForeignKey(x => x.AttributeValueId);
        });

        b.Entity<Product>().HasMany(p => p.Variants).WithOne(v => v.Product!).HasForeignKey(v => v.ProductId);
        b.Entity<Product>().HasMany(p => p.AttributeValues).WithOne(a => a.Product!).HasForeignKey(a => a.ProductId);

        // --- Import jobs ---
        b.Entity<ImportJob>(e => { e.ToTable("ImportJobs"); e.HasKey(x => x.ImportJobId); });
        b.Entity<ImportJobItem>(e => { e.ToTable("ImportJobItems"); e.HasKey(x => x.ImportJobItemId); });

        // --- Theme ---
        b.Entity<Theme>(e =>
        {
            e.ToTable("Themes");
            e.HasKey(x => x.ThemeId);
            e.HasMany(x => x.Settings).WithOne(s => s.Theme!).HasForeignKey(s => s.ThemeId);
        });
        b.Entity<ThemeSetting>(e => { e.ToTable("ThemeSettings"); e.HasKey(x => x.ThemeSettingId); });

        // --- CMS ---
        b.Entity<Page>(e =>
        {
            e.ToTable("Pages");
            e.HasKey(x => x.PageId);
            e.HasMany(x => x.Sections).WithOne(s => s.Page!).HasForeignKey(s => s.PageId);
        });
        b.Entity<PageSection>(e => { e.ToTable("PageSections"); e.HasKey(x => x.PageSectionId); });

        // --- Inventory & Search ---
        b.Entity<Inventory>(e =>
        {
            e.ToTable("Inventory");
            e.HasKey(x => x.InventoryId);
            e.HasOne(x => x.Product).WithMany(p => p.InventoryRecords).HasForeignKey(x => x.ProductId);
        });
        b.Entity<InventoryTransaction>(e => { e.ToTable("InventoryTransactions"); e.HasKey(x => x.InventoryTransactionId); });
        b.Entity<SearchLog>(e => { e.ToTable("SearchLogs"); e.HasKey(x => x.SearchLogId); });
        b.Entity<PopularSearch>(e => { e.ToTable("PopularSearches"); e.HasKey(x => x.PopularSearchId); });

        // --- Shopping ---
        b.Entity<CustomerAddress>(e => { e.ToTable("CustomerAddresses"); e.HasKey(x => x.CustomerAddressId); });
        b.Entity<Cart>(e =>
        {
            e.ToTable("Cart");
            e.HasKey(x => x.CartId);
            e.HasMany(x => x.Items).WithOne(i => i.Cart!).HasForeignKey(i => i.CartId);
        });
        b.Entity<CartItem>(e =>
        {
            e.ToTable("CartItems");
            e.HasKey(x => x.CartItemId);
            e.Property(x => x.UnitPrice).HasPrecision(12, 2);
            e.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId);
        });

        // --- Checkout / Orders / Billing ---
        b.Entity<Order>(e =>
        {
            e.ToTable("Orders");
            e.HasKey(x => x.OrderId);
            foreach (var p in new[] { nameof(Order.Subtotal), nameof(Order.DiscountAmount), nameof(Order.TaxAmount), nameof(Order.ShippingAmount), nameof(Order.TotalAmount) })
                e.Property(p).HasPrecision(12, 2);
            e.HasMany(x => x.Items).WithOne(i => i.Order!).HasForeignKey(i => i.OrderId);
        });
        b.Entity<OrderItem>(e =>
        {
            e.ToTable("OrderItems");
            e.HasKey(x => x.OrderItemId);
            foreach (var p in new[] { nameof(OrderItem.UnitPrice), nameof(OrderItem.UnitCost), nameof(OrderItem.DiscountAmount), nameof(OrderItem.TaxAmount), nameof(OrderItem.LineTotal) })
                e.Property(p).HasPrecision(12, 2);
            e.Property(x => x.TaxRate).HasPrecision(5, 2);
        });
        b.Entity<OrderStatusHistory>(e => { e.ToTable("OrderStatusHistory"); e.HasKey(x => x.OrderStatusHistoryId); });
        b.Entity<Payment>(e => { e.ToTable("Payments"); e.HasKey(x => x.PaymentId); e.Property(x => x.Amount).HasPrecision(12, 2); });
        b.Entity<PaymentTransaction>(e => { e.ToTable("PaymentTransactions"); e.HasKey(x => x.PaymentTransactionId); e.Property(x => x.Amount).HasPrecision(12, 2); });
        b.Entity<Refund>(e => { e.ToTable("Refunds"); e.HasKey(x => x.RefundId); e.Property(x => x.Amount).HasPrecision(12, 2); });
        b.Entity<TaxRate>(e =>
        {
            e.ToTable("TaxRates");
            e.HasKey(x => x.TaxRateId);
            foreach (var p in new[] { nameof(TaxRate.CgstRate), nameof(TaxRate.SgstRate), nameof(TaxRate.IgstRate), nameof(TaxRate.TotalRate) })
                e.Property(p).HasPrecision(5, 2);
        });
        b.Entity<ShippingMethod>(e =>
        {
            e.ToTable("ShippingMethods");
            e.HasKey(x => x.ShippingMethodId);
            e.Property(x => x.BaseRate).HasPrecision(12, 2);
            e.Property(x => x.FreeShippingThreshold).HasPrecision(12, 2);
        });
        b.Entity<ShippingZone>(e => { e.ToTable("ShippingZones"); e.HasKey(x => x.ShippingZoneId); e.Property(x => x.Rate).HasPrecision(12, 2); });
        b.Entity<Invoice>(e =>
        {
            e.ToTable("Invoices");
            e.HasKey(x => x.InvoiceId);
            foreach (var p in new[] { nameof(Invoice.Subtotal), nameof(Invoice.TaxAmount), nameof(Invoice.CgstAmount), nameof(Invoice.SgstAmount), nameof(Invoice.IgstAmount), nameof(Invoice.TotalAmount) })
                e.Property(p).HasPrecision(12, 2);
            e.HasMany(x => x.Items).WithOne(i => i.Invoice!).HasForeignKey(i => i.InvoiceId);
        });
        b.Entity<InvoiceItem>(e =>
        {
            e.ToTable("InvoiceItems");
            e.HasKey(x => x.InvoiceItemId);
            foreach (var p in new[] { nameof(InvoiceItem.UnitPrice), nameof(InvoiceItem.TaxAmount), nameof(InvoiceItem.LineTotal) })
                e.Property(p).HasPrecision(12, 2);
            e.Property(x => x.TaxRate).HasPrecision(5, 2);
        });
        b.Entity<Setting>(e => { e.ToTable("Settings"); e.HasKey(x => x.SettingId); });
        b.Entity<StateMinOrderAmount>(e =>
        {
            e.ToTable("StateMinOrderAmounts");
            e.HasKey(x => x.StateMinOrderAmountId);
            e.HasIndex(x => new { x.TenantId, x.StateName }).IsUnique();
        });
        b.Entity<HomeBanner>(e =>
        {
            e.ToTable("HomeBanners");
            e.HasKey(x => x.HomeBannerId);
            e.Property(x => x.ImageData).HasColumnType("LONGBLOB");
        });
        b.Entity<GalleryImage>(e =>
        {
            e.ToTable("GalleryImages");
            e.HasKey(x => x.GalleryImageId);
            e.Property(x => x.ImageData).HasColumnType("LONGBLOB");
        });
        b.Entity<MediaFile>(e => { e.ToTable("MediaFiles"); e.HasKey(x => x.MediaFileId); });
        b.Entity<NotificationTemplate>(e => { e.ToTable("NotificationTemplates"); e.HasKey(x => x.NotificationTemplateId); });
        b.Entity<NotificationHistory>(e => { e.ToTable("NotificationHistory"); e.HasKey(x => x.NotificationHistoryId); });
        b.Entity<Review>(e => { e.ToTable("Reviews"); e.HasKey(x => x.ReviewId); });
        b.Entity<Coupon>(e =>
        {
            e.ToTable("Coupons");
            e.HasKey(x => x.CouponId);
            foreach (var p in new[] { nameof(Coupon.DiscountValue), nameof(Coupon.MaxDiscountAmount), nameof(Coupon.MinOrderAmount) })
                e.Property(p).HasPrecision(12, 2);
        });
        b.Entity<CouponUsage>(e =>
        {
            e.ToTable("CouponUsage");
            e.HasKey(x => x.CouponUsageId);
            e.Property(x => x.DiscountAmount).HasPrecision(12, 2);
        });
        b.Entity<Shipment>(e => { e.ToTable("Shipments"); e.HasKey(x => x.ShipmentId); });
        b.Entity<WishlistItem>(e => { e.ToTable("WishlistItems"); e.HasKey(x => x.WishlistItemId); });
        b.Entity<Notification>(e => { e.ToTable("Notifications"); e.HasKey(x => x.NotificationId); });
        b.Entity<Supplier>(e => { e.ToTable("Suppliers"); e.HasKey(x => x.SupplierId); });
        b.Entity<Contact>(e => { e.ToTable("Contacts"); e.HasKey(x => x.ContactId); });
        b.Entity<Campaign>(e => { e.ToTable("Campaigns"); e.HasKey(x => x.CampaignId); });
        b.Entity<CampaignRecipient>(e => { e.ToTable("CampaignRecipients"); e.HasKey(x => x.CampaignRecipientId); });
        b.Entity<ProductSupplier>(e =>
        {
            e.ToTable("ProductSuppliers");
            e.HasKey(x => x.ProductSupplierId);
            e.Property(x => x.CostPrice).HasPrecision(12, 2);
        });
    }
}
