using System.Reflection;
using ecomm.api.Common.Tenancy;
using ecomm.api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Data.Context;

/// <summary>
/// Database-first context: the SQL migrations in /database/migrations are the
/// source of truth. Entities are hand-mapped to existing tables here (we do NOT
/// generate EF migrations). DbSets/entities grow per feature slice.
///
/// V2 multi-tenancy: every <see cref="ITenantScoped"/> entity gets an automatic
/// query filter (WHERE TenantId = current tenant) and is auto-stamped on insert.
/// </summary>
public class EcommerceDbContext : DbContext
{
    private readonly ICurrentTenantService _tenant;

    public EcommerceDbContext(DbContextOptions<EcommerceDbContext> options, ICurrentTenantService tenant) : base(options)
    {
        _tenant = tenant;
    }

    /// <summary>The tenant in effect for this context. Services use this instead of a hardcoded id.</summary>
    public long CurrentTenantId => _tenant.CurrentTenantId;

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
    public DbSet<UserTwoFactorBackupCode> UserTwoFactorBackupCodes => Set<UserTwoFactorBackupCode>();
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
    public DbSet<Collection> Collections => Set<Collection>();
    public DbSet<ProductCollection> ProductCollections => Set<ProductCollection>();
    public DbSet<Menu> Menus => Set<Menu>();
    public DbSet<UrlRedirect> UrlRedirects => Set<UrlRedirect>();
    public DbSet<StorePolicy> StorePolicies => Set<StorePolicy>();

    // --- Import jobs ---
    public DbSet<ImportJob> ImportJobs => Set<ImportJob>();
    public DbSet<ImportJobItem> ImportJobItems => Set<ImportJobItem>();

    // --- Theme ---
    public DbSet<Theme> Themes => Set<Theme>();
    public DbSet<ThemeSetting> ThemeSettings => Set<ThemeSetting>();
    public DbSet<ThemeTemplate> ThemeTemplates => Set<ThemeTemplate>();
    public DbSet<ThemeSection> ThemeSections => Set<ThemeSection>();

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
    public DbSet<CustomerProfile> CustomerProfiles => Set<CustomerProfile>();
    public DbSet<TenantStaff> TenantStaff => Set<TenantStaff>();
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
    public DbSet<HomeBanner> HomeBanners => Set<HomeBanner>();
    public DbSet<MediaFile> MediaFiles => Set<MediaFile>();
    public DbSet<NotificationTemplate> NotificationTemplates => Set<NotificationTemplate>();
    public DbSet<NotificationHistory> NotificationHistory => Set<NotificationHistory>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Coupon> Coupons => Set<Coupon>();
    public DbSet<CouponUsage> CouponUsages => Set<CouponUsage>();
    public DbSet<CouponTarget> CouponTargets => Set<CouponTarget>();
    public DbSet<Shipment> Shipments => Set<Shipment>();
    public DbSet<WishlistItem> WishlistItems => Set<WishlistItem>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<ProductSupplier> ProductSuppliers => Set<ProductSupplier>();
    public DbSet<ColorSwatch> ColorSwatches => Set<ColorSwatch>();
    public DbSet<BundleItem> BundleItems => Set<BundleItem>();
    public DbSet<UserNotificationPreference> UserNotificationPreferences => Set<UserNotificationPreference>();
    public DbSet<ChatbotConversationState> ChatbotConversationStates => Set<ChatbotConversationState>();
    public DbSet<ChatbotUnansweredQuestion> ChatbotUnansweredQuestions => Set<ChatbotUnansweredQuestion>();
    public DbSet<PricingSeasonRule> PricingSeasonRules => Set<PricingSeasonRule>();
    public DbSet<PriceSuggestion> PriceSuggestions => Set<PriceSuggestion>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<WebhookSubscription> WebhookSubscriptions => Set<WebhookSubscription>();
    public DbSet<WebhookDelivery> WebhookDeliveries => Set<WebhookDelivery>();

    // --- V2: Plans & Subscriptions ---
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<TenantSubscription> TenantSubscriptions => Set<TenantSubscription>();
    public DbSet<TenantBillingHistory> TenantBillingHistory => Set<TenantBillingHistory>();
    public DbSet<TenantSetting> TenantSettings => Set<TenantSetting>();
    public DbSet<TenantPaymentAccount> TenantPaymentAccounts => Set<TenantPaymentAccount>();
    public DbSet<TenantShippingAccount> TenantShippingAccounts => Set<TenantShippingAccount>();
    public DbSet<PlatformAccessLog> PlatformAccessLog => Set<PlatformAccessLog>();
    public DbSet<TenantNote> TenantNotes => Set<TenantNote>();
    public DbSet<PlatformAnnouncement> PlatformAnnouncements => Set<PlatformAnnouncement>();
    public DbSet<PlatformPaymentSetting> PlatformPaymentSettings => Set<PlatformPaymentSetting>();
    public DbSet<PlatformEmailSetting> PlatformEmailSettings => Set<PlatformEmailSetting>();
    public DbSet<BackInStockRequest> BackInStockRequests => Set<BackInStockRequest>();
    public DbSet<NewsletterSubscriber> NewsletterSubscribers => Set<NewsletterSubscriber>();
    public DbSet<SupportTicket> SupportTickets => Set<SupportTicket>();
    public DbSet<SupportMessage> SupportMessages => Set<SupportMessage>();
    public DbSet<SupportTicketActivity> SupportTicketActivities => Set<SupportTicketActivity>();
    public DbSet<SignupBlocklist> SignupBlocklist => Set<SignupBlocklist>();
    public DbSet<ContactMessage> ContactMessages => Set<ContactMessage>();
    public DbSet<ShipmentCheckpoint> ShipmentCheckpoints => Set<ShipmentCheckpoint>();
    public DbSet<Faq> Faqs => Set<Faq>();
    public DbSet<GrowthBrandKit> GrowthBrandKits => Set<GrowthBrandKit>();
    public DbSet<MarketingBrandProfile> MarketingBrandProfiles => Set<MarketingBrandProfile>();
    public DbSet<SocialConnection> SocialConnections => Set<SocialConnection>();
    public DbSet<GrowthContent> GrowthContents => Set<GrowthContent>();
    public DbSet<GrowthCampaign> GrowthCampaigns => Set<GrowthCampaign>();
    public DbSet<GrowthFestival> GrowthFestivals => Set<GrowthFestival>();
    public DbSet<Article> Articles => Set<Article>();
    public DbSet<CustomerEvent> CustomerEvents => Set<CustomerEvent>();
    public DbSet<PlatformBillingSettings> PlatformBillingSettings => Set<PlatformBillingSettings>();
    public DbSet<PlatformInvoice> PlatformInvoices => Set<PlatformInvoice>();
    public DbSet<App> Apps => Set<App>();
    public DbSet<AppInstallation> AppInstallations => Set<AppInstallation>();
    public DbSet<AppOAuthCode> AppOAuthCodes => Set<AppOAuthCode>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<AppCharge> AppCharges => Set<AppCharge>();

    // --- V2: AI credits (AI-0) ---
    public DbSet<TenantAiCredit> TenantAiCredits => Set<TenantAiCredit>();
    public DbSet<AiUsageLog> AiUsageLogs => Set<AiUsageLog>();
    public DbSet<AiCreditPack> AiCreditPacks => Set<AiCreditPack>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Tenant>(e => { e.ToTable("Tenants"); e.HasKey(x => x.TenantId); });

        b.Entity<User>(e =>
        {
            e.ToTable("Users");
            e.HasKey(x => x.UserId);
        });

        b.Entity<UserTwoFactorBackupCode>(e =>
        {
            e.ToTable("UserTwoFactorBackupCodes");
            e.HasKey(x => x.UserTwoFactorBackupCodeId);
            e.HasOne(x => x.User).WithMany(u => u.TwoFactorBackupCodes).HasForeignKey(x => x.UserId);
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

        b.Entity<Collection>(e =>
        {
            e.ToTable("Collections");
            e.HasKey(x => x.CollectionId);
            e.Property(x => x.RulesJson).HasColumnType("json");
        });
        b.Entity<ProductCollection>(e => { e.ToTable("ProductCollections"); e.HasKey(x => x.ProductCollectionId); });
        b.Entity<Menu>(e => { e.ToTable("Menus"); e.HasKey(x => x.MenuId); e.Property(x => x.ItemsJson).HasColumnType("json"); });
        b.Entity<UrlRedirect>(e => { e.ToTable("UrlRedirects"); e.HasKey(x => x.UrlRedirectId); });
        b.Entity<StorePolicy>(e => { e.ToTable("StorePolicies"); e.HasKey(x => x.StorePolicyId); });

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
        b.Entity<ThemeTemplate>(e =>
        {
            e.ToTable("ThemeTemplates");
            e.HasKey(x => x.ThemeTemplateId);
            e.HasMany(x => x.Sections).WithOne(s => s.Template!).HasForeignKey(s => s.ThemeTemplateId);
        });
        b.Entity<ThemeSection>(e =>
        {
            e.ToTable("ThemeSections");
            e.HasKey(x => x.ThemeSectionId);
            e.Property(x => x.Settings).HasColumnType("json");
            e.Property(x => x.Blocks).HasColumnType("json");
        });

        // --- CMS ---
        b.Entity<Page>(e =>
        {
            e.ToTable("Pages");
            e.HasKey(x => x.PageId);
            e.HasMany(x => x.Sections).WithOne(s => s.Page!).HasForeignKey(s => s.PageId);
        });
        b.Entity<PageSection>(e =>
        {
            e.ToTable("PageSections");
            e.HasKey(x => x.PageSectionId);
            e.Property(x => x.Settings).HasColumnType("json");
            e.Property(x => x.Blocks).HasColumnType("json");
        });

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
        b.Entity<CustomerProfile>(e => { e.ToTable("CustomerProfiles"); e.HasKey(x => x.CustomerProfileId); });
        b.Entity<TenantStaff>(e => { e.ToTable("TenantStaff"); e.HasKey(x => x.TenantStaffId); });
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
        b.Entity<HomeBanner>(e =>
        {
            e.ToTable("HomeBanners");
            e.HasKey(x => x.HomeBannerId);
            e.Property(x => x.ImageData).HasColumnType("LONGBLOB");
        });
        b.Entity<MediaFile>(e => { e.ToTable("MediaFiles"); e.HasKey(x => x.MediaFileId); });
        b.Entity<NotificationTemplate>(e => { e.ToTable("NotificationTemplates"); e.HasKey(x => x.NotificationTemplateId); });
        b.Entity<NotificationHistory>(e => { e.ToTable("NotificationHistory"); e.HasKey(x => x.NotificationHistoryId); });
        b.Entity<UserNotificationPreference>(e =>
        {
            e.ToTable("UserNotificationPreferences");
            e.HasKey(x => x.UserNotificationPreferenceId);
            e.HasIndex(x => new { x.TenantId, x.UserId, x.Channel, x.Category }).IsUnique();
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<ChatbotConversationState>(e =>
        {
            e.ToTable("ChatbotConversationStates");
            e.HasKey(x => x.ChatbotConversationStateId);
            e.HasIndex(x => x.SupportTicketId).IsUnique();
        });
        b.Entity<ChatbotUnansweredQuestion>(e => { e.ToTable("ChatbotUnansweredQuestions"); e.HasKey(x => x.ChatbotUnansweredQuestionId); });
        b.Entity<PricingSeasonRule>(e =>
        {
            e.ToTable("PricingSeasonRules");
            e.HasKey(x => x.PricingSeasonRuleId);
            e.Property(x => x.BiasPercent).HasPrecision(5, 2);
        });
        b.Entity<PriceSuggestion>(e =>
        {
            e.ToTable("PriceSuggestions");
            e.HasKey(x => x.PriceSuggestionId);
            foreach (var p in new[] { nameof(PriceSuggestion.InventorySignalPercent), nameof(PriceSuggestion.DemandSignalPercent), nameof(PriceSuggestion.SeasonalitySignalPercent) })
                e.Property(p).HasPrecision(5, 2);
            e.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<ApiKey>(e =>
        {
            e.ToTable("ApiKeys");
            e.HasKey(x => x.ApiKeyId);
            e.HasIndex(x => x.KeyHash).IsUnique();
        });
        b.Entity<WebhookSubscription>(e => { e.ToTable("WebhookSubscriptions"); e.HasKey(x => x.WebhookSubscriptionId); });
        b.Entity<WebhookDelivery>(e =>
        {
            e.ToTable("WebhookDeliveries");
            e.HasKey(x => x.WebhookDeliveryId);
            e.HasOne(x => x.Subscription).WithMany().HasForeignKey(x => x.WebhookSubscriptionId).OnDelete(DeleteBehavior.Cascade);
        });
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
        b.Entity<CouponTarget>(e => { e.ToTable("CouponTargets"); e.HasKey(x => x.CouponTargetId); });
        b.Entity<BundleItem>(e => { e.ToTable("BundleItems"); e.HasKey(x => x.BundleItemId); });
        b.Entity<Shipment>(e => { e.ToTable("Shipments"); e.HasKey(x => x.ShipmentId); });
        b.Entity<WishlistItem>(e => { e.ToTable("WishlistItems"); e.HasKey(x => x.WishlistItemId); });
        b.Entity<Notification>(e => { e.ToTable("Notifications"); e.HasKey(x => x.NotificationId); });
        b.Entity<Supplier>(e => { e.ToTable("Suppliers"); e.HasKey(x => x.SupplierId); });
        b.Entity<ProductSupplier>(e =>
        {
            e.ToTable("ProductSuppliers");
            e.HasKey(x => x.ProductSupplierId);
            e.Property(x => x.CostPrice).HasPrecision(12, 2);
        });
        b.Entity<ColorSwatch>(e => { e.ToTable("ColorSwatches"); e.HasKey(x => x.ColorSwatchId); });

        // --- V2: Plans & Subscriptions ---
        b.Entity<Plan>(e =>
        {
            e.ToTable("Plans");
            e.HasKey(x => x.PlanId);
            e.Property(x => x.MonthlyPrice).HasPrecision(10, 2);
        });
        b.Entity<TenantSubscription>(e =>
        {
            e.ToTable("TenantSubscriptions");
            e.HasKey(x => x.TenantSubscriptionId);
            e.HasOne(x => x.Plan).WithMany().HasForeignKey(x => x.PlanId);
        });
        b.Entity<TenantBillingHistory>(e =>
        {
            e.ToTable("TenantBillingHistory");
            e.HasKey(x => x.TenantBillingHistoryId);
            e.Property(x => x.Amount).HasPrecision(10, 2);
        });
        b.Entity<TenantSetting>(e => { e.ToTable("TenantSettings"); e.HasKey(x => x.TenantSettingId); });
        b.Entity<TenantPaymentAccount>(e => { e.ToTable("TenantPaymentAccounts"); e.HasKey(x => x.TenantPaymentAccountId); });
        b.Entity<TenantShippingAccount>(e => { e.ToTable("TenantShippingAccounts"); e.HasKey(x => x.TenantShippingAccountId); });
        b.Entity<PlatformAccessLog>(e => { e.ToTable("PlatformAccessLog"); e.HasKey(x => x.PlatformAccessLogId); });
        b.Entity<TenantNote>(e => { e.ToTable("TenantNotes"); e.HasKey(x => x.TenantNoteId); });
        b.Entity<PlatformAnnouncement>(e => { e.ToTable("PlatformAnnouncements"); e.HasKey(x => x.PlatformAnnouncementId); });
        b.Entity<PlatformPaymentSetting>(e => { e.ToTable("PlatformPaymentSettings"); e.HasKey(x => x.PlatformPaymentSettingId); });
        b.Entity<PlatformEmailSetting>(e => { e.ToTable("PlatformEmailSettings"); e.HasKey(x => x.PlatformEmailSettingId); });
        b.Entity<BackInStockRequest>(e => { e.ToTable("BackInStockRequests"); e.HasKey(x => x.BackInStockRequestId); });
        b.Entity<NewsletterSubscriber>(e => { e.ToTable("NewsletterSubscribers"); e.HasKey(x => x.NewsletterSubscriberId); });
        b.Entity<SupportTicket>(e => { e.ToTable("SupportTickets"); e.HasKey(x => x.SupportTicketId); });
        b.Entity<SupportMessage>(e => { e.ToTable("SupportMessages"); e.HasKey(x => x.SupportMessageId); });
        b.Entity<SupportTicketActivity>(e => { e.ToTable("SupportTicketActivities"); e.HasKey(x => x.SupportTicketActivityId); });
        b.Entity<SignupBlocklist>(e => { e.ToTable("SignupBlocklist"); e.HasKey(x => x.SignupBlocklistId); });
        b.Entity<ContactMessage>(e => { e.ToTable("ContactMessages"); e.HasKey(x => x.ContactMessageId); });
        b.Entity<ShipmentCheckpoint>(e => { e.ToTable("ShipmentCheckpoints"); e.HasKey(x => x.ShipmentCheckpointId); });
        b.Entity<Faq>(e => { e.ToTable("Faqs"); e.HasKey(x => x.FaqId); });
        b.Entity<GrowthBrandKit>(e => { e.ToTable("GrowthBrandKits"); e.HasKey(x => x.GrowthBrandKitId); });
        b.Entity<MarketingBrandProfile>(e => { e.ToTable("MarketingBrandProfiles"); e.HasKey(x => x.MarketingBrandProfileId); });
        b.Entity<SocialConnection>(e => { e.ToTable("SocialConnections"); e.HasKey(x => x.SocialConnectionId); });
        b.Entity<GrowthContent>(e => { e.ToTable("GrowthContents"); e.HasKey(x => x.GrowthContentId); });
        b.Entity<GrowthCampaign>(e => { e.ToTable("GrowthCampaigns"); e.HasKey(x => x.GrowthCampaignId); });
        // Global (not tenant-scoped): the festival calendar is shared across all stores.
        b.Entity<GrowthFestival>(e => { e.ToTable("GrowthFestivals"); e.HasKey(x => x.GrowthFestivalId); e.Property(x => x.Date).HasColumnType("date"); });
        b.Entity<Article>(e => { e.ToTable("Articles"); e.HasKey(x => x.ArticleId); });
        b.Entity<CustomerEvent>(e => { e.ToTable("CustomerEvents"); e.HasKey(x => x.CustomerEventId); });
        // Global (not tenant-scoped): platform-issued GST invoices numbered per the platform's GSTIN.
        b.Entity<PlatformBillingSettings>(e => { e.ToTable("PlatformBillingSettings"); e.HasKey(x => x.PlatformBillingSettingsId); });
        b.Entity<PlatformInvoice>(e => { e.ToTable("PlatformInvoices"); e.HasKey(x => x.PlatformInvoiceId); });
        // App marketplace: App + AppOAuthCode are global; AppInstallation is tenant-scoped (auto-filtered).
        b.Entity<App>(e => { e.ToTable("Apps"); e.HasKey(x => x.AppId); });
        b.Entity<AppInstallation>(e => { e.ToTable("AppInstallations"); e.HasKey(x => x.AppInstallationId); });
        b.Entity<AppOAuthCode>(e => { e.ToTable("AppOAuthCodes"); e.HasKey(x => x.AppOAuthCodeId); });
        b.Entity<AppSetting>(e => { e.ToTable("AppSettings"); e.HasKey(x => x.AppSettingId); });
        b.Entity<AppCharge>(e => { e.ToTable("AppCharges"); e.HasKey(x => x.AppChargeId); });
        b.Entity<TenantAiCredit>(e => { e.ToTable("TenantAiCredits"); e.HasKey(x => x.TenantAiCreditId); });
        b.Entity<AiUsageLog>(e => { e.ToTable("AiUsageLogs"); e.HasKey(x => x.AiUsageLogId); });
        b.Entity<AiCreditPack>(e => { e.ToTable("AiCreditPacks"); e.HasKey(x => x.AiCreditPackId); e.Property(x => x.PriceInr).HasPrecision(10, 2); });

        // --- Multi-tenant global query filters (V2-0) ---
        // Every ITenantScoped entity is auto-scoped to the current tenant. Read live
        // (_tenant.CurrentTenantId is evaluated per query), so it reflects the tenant
        // the middleware resolved for the request. Tenant/Role are NOT ITenantScoped.
        foreach (var et in b.Model.GetEntityTypes())
        {
            if (typeof(ITenantScoped).IsAssignableFrom(et.ClrType))
                ApplyTenantFilterMethod.MakeGenericMethod(et.ClrType).Invoke(this, new object[] { b });
        }
    }

    private static readonly MethodInfo ApplyTenantFilterMethod =
        typeof(EcommerceDbContext).GetMethod(nameof(ApplyTenantFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;

    private void ApplyTenantFilter<T>(ModelBuilder b) where T : class, ITenantScoped
        => b.Entity<T>().HasQueryFilter(e => e.TenantId == _tenant.CurrentTenantId);

    // Auto-stamp TenantId on every inserted tenant-scoped row so a write can never
    // land in the wrong (or a forgotten) tenant. Cross-tenant writes must go through
    // ICurrentTenantService.BeginScope (jobs/seeders/super-admin).
    private void StampTenant()
    {
        var tenantId = _tenant.CurrentTenantId;
        foreach (var entry in ChangeTracker.Entries<ITenantScoped>())
            if (entry.State == EntityState.Added)
                entry.Entity.TenantId = tenantId;
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampTenant();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StampTenant();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }
}
