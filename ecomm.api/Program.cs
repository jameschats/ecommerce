using System.Text;
using System.Threading.RateLimiting;
using ecomm.api.Common.Middleware;
using ecomm.api.Common.Tenancy;
using Microsoft.AspNetCore.HttpOverrides;
using ecomm.api.Data.Context;
using Serilog.Core;
using ecomm.api.Features.Auth;
using ecomm.api.Features.Auth.Services;
using ecomm.api.Features.Payments;
using Hangfire;
using Hangfire.Dashboard;
using Hangfire.MySql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Serilog;

// QuestPDF community licence (free for invoice PDF generation)
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

// --- Logging (Serilog) ----------------------------------------------------
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

// --- Services -------------------------------------------------------------
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Multi-tenancy (V2-0): tenant context + resolution + request tracing.
builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();
builder.Services.Configure<TenancyOptions>(builder.Configuration.GetSection(TenancyOptions.SectionName));
builder.Services.AddScoped<ICurrentTenantService, CurrentTenantService>();
builder.Services.AddSingleton<ILogEventEnricher, HttpContextLogEnricher>();   // CorrelationId + TenantId on every log

// CORS for the Angular app (ecomm.web). Multi-tenant: allow the configured apex
// origin AND any subdomain of the base domain (so {slug}.<domain> can call the API).
const string AngularCors = "AngularCors";
var angularOrigin = builder.Configuration["Cors:AngularOrigin"] ?? "http://localhost:4200";
var corsBaseDomain = builder.Configuration["Tenancy:BaseDomain"] ?? "";
builder.Services.AddCors(options =>
    options.AddPolicy(AngularCors, policy =>
        policy.SetIsOriginAllowed(origin =>
              {
                  if (string.Equals(origin, angularOrigin, StringComparison.OrdinalIgnoreCase)) return true;
                  if (string.IsNullOrEmpty(corsBaseDomain) || !Uri.TryCreate(origin, UriKind.Absolute, out var uri)) return false;
                  return uri.Host.Equals(corsBaseDomain, StringComparison.OrdinalIgnoreCase)
                      || uri.Host.EndsWith("." + corsBaseDomain, StringComparison.OrdinalIgnoreCase);
              })
              .AllowAnyHeader().AllowAnyMethod()));

// EF Core (MySQL, database-first via Pomelo)
var connectionString = builder.Configuration.GetConnectionString("Default");
builder.Services.AddDbContext<EcommerceDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

// Hangfire (v4 Phase 0): replaces the ad-hoc BackgroundService-timer pattern for scheduled/recurring
// work — see documents/v4-stages/phase-0-portability.md. Same connection string and driver family
// (MySqlConnector) as the EF Core context above, so this introduces no new storage dependency. Tables
// are prefixed so they read as clearly belonging to the library, not the app schema. Runs in-process
// for now; split into a dedicated worker only if job volume ever justifies it.
builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseStorage(new MySqlStorage(connectionString, new MySqlStorageOptions
    {
        TablesPrefix = "Hangfire_",
        PrepareSchemaIfNecessary = true,
    })));
builder.Services.AddHangfireServer();

// Auth: settings + services
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));
builder.Services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
// SMS sender selected by Sms:Provider (Logging dev-stub | Msg91 real). Powers OTP login + order SMS.
builder.Services.Configure<SmsOptions>(builder.Configuration.GetSection(SmsOptions.SectionName));
var smsProvider = builder.Configuration["Sms:Provider"] ?? "Logging";
if (smsProvider.Equals("Msg91", StringComparison.OrdinalIgnoreCase))
    builder.Services.AddScoped<ISmsSender>(sp => new Msg91SmsSender(
        sp.GetRequiredService<IHttpClientFactory>().CreateClient("msg91"),
        sp.GetRequiredService<IOptions<SmsOptions>>(),
        sp.GetRequiredService<ILogger<Msg91SmsSender>>()));
else
    builder.Services.AddScoped<ISmsSender, ConsoleSmsSender>();
builder.Services.AddScoped<IOtpService, OtpService>();
builder.Services.AddScoped<IGoogleTokenValidator, GoogleTokenValidator>();
builder.Services.AddScoped<IAuthProviderService, AuthProviderService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ecomm.api.Features.Auth.Services.ITwoFactorService, ecomm.api.Features.Auth.Services.TwoFactorService>();
builder.Services.AddScoped<ecomm.api.Features.Onboarding.IOnboardingService, ecomm.api.Features.Onboarding.OnboardingService>();
builder.Services.AddScoped<ecomm.api.Features.Subscriptions.ISubscriptionService, ecomm.api.Features.Subscriptions.SubscriptionService>();
builder.Services.AddScoped<ecomm.api.Features.Subscriptions.IRazorpayWebhookService, ecomm.api.Features.Subscriptions.RazorpayWebhookService>();
builder.Services.AddScoped<ecomm.api.Features.SuperAdmin.ISuperAdminService, ecomm.api.Features.SuperAdmin.SuperAdminService>();
builder.Services.AddScoped<ecomm.api.Features.SuperAdmin.IPlatformStaffService, ecomm.api.Features.SuperAdmin.PlatformStaffService>();
builder.Services.AddScoped<ecomm.api.Features.SuperAdmin.IPlatformAnnouncementService, ecomm.api.Features.SuperAdmin.PlatformAnnouncementService>();
builder.Services.AddScoped<ecomm.api.Features.Support.ISupportService, ecomm.api.Features.Support.SupportService>();
builder.Services.AddHostedService<AdminUserSeeder>();

// Catalog
builder.Services.AddScoped<ecomm.api.Features.Catalog.Services.ICategoryService, ecomm.api.Features.Catalog.Services.CategoryService>();
builder.Services.AddScoped<ecomm.api.Features.Catalog.Services.IBrandService, ecomm.api.Features.Catalog.Services.BrandService>();
builder.Services.AddScoped<ecomm.api.Features.Catalog.Services.IProductService, ecomm.api.Features.Catalog.Services.ProductService>();
builder.Services.AddScoped<ecomm.api.Features.Catalog.Services.IVariantService, ecomm.api.Features.Catalog.Services.VariantService>();
builder.Services.AddScoped<ecomm.api.Features.Catalog.Services.IAttributeService, ecomm.api.Features.Catalog.Services.AttributeService>();
builder.Services.AddScoped<ecomm.api.Features.Catalog.Services.IProductAttributeService, ecomm.api.Features.Catalog.Services.ProductAttributeService>();
builder.Services.AddScoped<ecomm.api.Features.Catalog.Services.IProductImportService, ecomm.api.Features.Catalog.Services.ProductImportService>();

// Theme
builder.Services.AddScoped<ecomm.api.Features.Theme.IThemeService, ecomm.api.Features.Theme.ThemeService>();

// CMS
builder.Services.AddScoped<ecomm.api.Features.Cms.ICmsService, ecomm.api.Features.Cms.CmsService>();
builder.Services.AddScoped<ecomm.api.Features.Cms.IBannerService, ecomm.api.Features.Cms.BannerService>();
builder.Services.Configure<ecomm.api.Features.Media.MediaOptions>(builder.Configuration.GetSection(ecomm.api.Features.Media.MediaOptions.SectionName));
builder.Services.AddSingleton<ecomm.api.Features.Media.IMediaStorage, ecomm.api.Features.Media.LocalDiskStorage>();
builder.Services.AddSingleton<ecomm.api.Features.Media.IImageVariantService, ecomm.api.Features.Media.ImageVariantService>();
builder.Services.AddScoped<ecomm.api.Features.Media.IMediaService, ecomm.api.Features.Media.MediaService>();

// Notifications — email sender selected by Email:Provider (Logging dev-stub | Smtp real).
builder.Services.Configure<ecomm.api.Features.Notifications.EmailOptions>(builder.Configuration.GetSection(ecomm.api.Features.Notifications.EmailOptions.SectionName));
var emailProvider = builder.Configuration["Email:Provider"] ?? "Logging";
if (emailProvider.Equals("Smtp", StringComparison.OrdinalIgnoreCase))
    builder.Services.AddScoped<ecomm.api.Features.Notifications.IEmailSender, ecomm.api.Features.Notifications.SmtpEmailSender>();
else
    builder.Services.AddScoped<ecomm.api.Features.Notifications.IEmailSender, ecomm.api.Features.Notifications.LoggingEmailSender>();
// Channel router: code -> primary channel + fallback chain (v4 Phase 1 Track A). One
// INotificationChannel per transport, wrapping the senders above — WhatsApp/Push join this list
// once built, no other change needed here.
builder.Services.AddScoped<ecomm.api.Features.Notifications.INotificationChannel, ecomm.api.Features.Notifications.EmailNotificationChannel>();
builder.Services.AddScoped<ecomm.api.Features.Notifications.INotificationChannel, ecomm.api.Features.Notifications.SmsNotificationChannel>();
// WhatsApp provider selected by WhatsApp:Provider (None dev-stub | Gupshup | Interakt) — v4 Phase 1
// Track C. Interakt added as a parallel fallback while Gupshup's own signup flow had operational
// problems — see phase-1-notifications-2fa.md's Track C notes.
builder.Services.Configure<ecomm.api.Features.WhatsApp.WhatsAppOptions>(builder.Configuration.GetSection(ecomm.api.Features.WhatsApp.WhatsAppOptions.SectionName));
var whatsAppProvider = builder.Configuration["WhatsApp:Provider"] ?? "None";
if (whatsAppProvider.Equals("Gupshup", StringComparison.OrdinalIgnoreCase))
    builder.Services.AddScoped<ecomm.api.Features.WhatsApp.IWhatsAppProvider>(sp => new ecomm.api.Features.WhatsApp.GupshupWhatsAppProvider(
        sp.GetRequiredService<IHttpClientFactory>().CreateClient("gupshup"),
        sp.GetRequiredService<IOptions<ecomm.api.Features.WhatsApp.WhatsAppOptions>>(),
        sp.GetRequiredService<ILogger<ecomm.api.Features.WhatsApp.GupshupWhatsAppProvider>>()));
else if (whatsAppProvider.Equals("Interakt", StringComparison.OrdinalIgnoreCase))
    builder.Services.AddScoped<ecomm.api.Features.WhatsApp.IWhatsAppProvider>(sp => new ecomm.api.Features.WhatsApp.InteraktWhatsAppProvider(
        sp.GetRequiredService<IHttpClientFactory>().CreateClient("interakt"),
        sp.GetRequiredService<IOptions<ecomm.api.Features.WhatsApp.WhatsAppOptions>>(),
        sp.GetRequiredService<ILogger<ecomm.api.Features.WhatsApp.InteraktWhatsAppProvider>>()));
else
    builder.Services.AddScoped<ecomm.api.Features.WhatsApp.IWhatsAppProvider, ecomm.api.Features.WhatsApp.LoggingWhatsAppProvider>();
builder.Services.AddScoped<ecomm.api.Features.Notifications.INotificationChannel, ecomm.api.Features.Notifications.WhatsAppNotificationChannel>();
builder.Services.AddSingleton<ecomm.api.Features.Notifications.IBackgroundJobScheduler, ecomm.api.Features.Notifications.HangfireBackgroundJobScheduler>();
builder.Services.AddScoped<ecomm.api.Features.Notifications.INotificationRouter, ecomm.api.Features.Notifications.NotificationRouter>();
builder.Services.AddScoped<ecomm.api.Features.Notifications.INotificationService, ecomm.api.Features.Notifications.NotificationService>();
builder.Services.AddSignalR();
builder.Services.AddScoped<ecomm.api.Features.Notifications.INotificationFeedService, ecomm.api.Features.Notifications.NotificationFeedService>();
builder.Services.AddScoped<ecomm.api.Features.Notifications.INotificationAdminService, ecomm.api.Features.Notifications.NotificationAdminService>();
builder.Services.AddScoped<ecomm.api.Features.Reviews.IReviewService, ecomm.api.Features.Reviews.ReviewService>();
builder.Services.AddScoped<ecomm.api.Features.Coupons.ICouponService, ecomm.api.Features.Coupons.CouponService>();
builder.Services.AddScoped<ecomm.api.Features.Wishlist.IWishlistService, ecomm.api.Features.Wishlist.WishlistService>();
builder.Services.AddScoped<ecomm.api.Features.Analytics.IAnalyticsService, ecomm.api.Features.Analytics.AnalyticsService>();
builder.Services.AddScoped<ecomm.api.Features.Suppliers.ISupplierService, ecomm.api.Features.Suppliers.SupplierService>();
builder.Services.AddScoped<ecomm.api.Features.ColorSwatches.IColorSwatchService, ecomm.api.Features.ColorSwatches.ColorSwatchService>();
builder.Services.AddScoped<ecomm.api.Features.Catalog.Services.IBundleService, ecomm.api.Features.Catalog.Services.BundleService>();

// Inventory & Search
builder.Services.AddScoped<ecomm.api.Features.Inventory.IInventoryService, ecomm.api.Features.Inventory.InventoryService>();
builder.Services.AddScoped<ecomm.api.Features.Inventory.IInventoryImportService, ecomm.api.Features.Inventory.InventoryImportService>();
builder.Services.AddScoped<ecomm.api.Features.Search.ISearchService, ecomm.api.Features.Search.SearchService>();

// Shopping (Stage 4)
builder.Services.AddScoped<ecomm.api.Features.Account.IAccountService, ecomm.api.Features.Account.AccountService>();
builder.Services.AddScoped<ecomm.api.Features.Cart.ICartService, ecomm.api.Features.Cart.CartService>();
builder.Services.AddScoped<ecomm.api.Features.Pricing.IPricingControlsService, ecomm.api.Features.Pricing.PricingControlsService>();
builder.Services.AddScoped<ecomm.api.Features.Pricing.IPricingEngineService, ecomm.api.Features.Pricing.PricingEngineService>();
builder.Services.AddScoped<ecomm.api.Features.Pricing.IPricingSuggestionService, ecomm.api.Features.Pricing.PricingSuggestionService>();

// Checkout & Money (Stage 5)
builder.Services.Configure<PaymentOptions>(builder.Configuration.GetSection(PaymentOptions.SectionName));
builder.Services.AddHttpClient();
builder.Services.AddScoped<ecomm.api.Features.Domains.IDomainService, ecomm.api.Features.Domains.DomainService>();
// Short-timeout client for verifying a merchant's custom domain routes to us.
// Follow a couple of redirects so an edge http→https upgrade (e.g. Cloudflare) still resolves.
builder.Services.AddHttpClient("domain-verify", c => c.Timeout = TimeSpan.FromSeconds(5))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = true, MaxAutomaticRedirections = 3 });
builder.Services.AddScoped<ecomm.api.Features.Checkout.ITaxService, ecomm.api.Features.Checkout.TaxService>();
builder.Services.AddScoped<ecomm.api.Features.Checkout.IShippingService, ecomm.api.Features.Checkout.ShippingService>();
builder.Services.AddScoped<ecomm.api.Features.Orders.IInvoiceService, ecomm.api.Features.Orders.InvoiceService>();
builder.Services.AddScoped<ecomm.api.Features.Orders.IOrderService, ecomm.api.Features.Orders.OrderService>();
builder.Services.AddScoped<ecomm.api.Features.Orders.IDraftOrderService, ecomm.api.Features.Orders.DraftOrderService>();
builder.Services.AddScoped<ecomm.api.Features.Orders.ITestOrderService, ecomm.api.Features.Orders.TestOrderService>();
builder.Services.AddScoped<ecomm.api.Features.Contact.IContactService, ecomm.api.Features.Contact.ContactService>();
builder.Services.AddScoped<ecomm.api.Features.Support.IShopperConversationService, ecomm.api.Features.Support.ShopperConversationService>();
builder.Services.AddScoped<ecomm.api.Features.Support.IHelpdeskSettingsService, ecomm.api.Features.Support.HelpdeskSettingsService>();
builder.Services.AddScoped<ecomm.api.Features.Support.IChatbotService, ecomm.api.Features.Support.ChatbotService>();
builder.Services.AddScoped<ecomm.api.Features.Orders.IOrderLookupService, ecomm.api.Features.Orders.OrderLookupService>();
builder.Services.AddScoped<ecomm.api.Features.Faqs.IFaqService, ecomm.api.Features.Faqs.FaqService>();
builder.Services.AddScoped<ecomm.api.Features.Support.ISupportDraftService, ecomm.api.Features.Support.SupportDraftService>();
builder.Services.AddScoped<ecomm.api.Features.Notifications.IConversationRealtime, ecomm.api.Features.Notifications.ConversationRealtime>();
builder.Services.AddScoped<ecomm.api.Features.Plans.IEntitlementService, ecomm.api.Features.Plans.EntitlementService>();
builder.Services.AddScoped<ecomm.api.Features.Growth.IBrandKitService, ecomm.api.Features.Growth.BrandKitService>();
builder.Services.AddScoped<ecomm.api.Features.Growth.IGrowthGenerationService, ecomm.api.Features.Growth.GrowthGenerationService>();
builder.Services.AddScoped<ecomm.api.Features.Growth.IGrowthCampaignService, ecomm.api.Features.Growth.GrowthCampaignService>();
builder.Services.AddScoped<ecomm.api.Features.Growth.IGrowthImageService, ecomm.api.Features.Growth.GrowthImageService>();
builder.Services.AddScoped<ecomm.api.Features.Growth.ICatalogImageService, ecomm.api.Features.Growth.CatalogImageService>();
builder.Services.AddScoped<ecomm.api.Features.Settings.IStoreSettingsService, ecomm.api.Features.Settings.StoreSettingsService>();
builder.Services.AddScoped<ecomm.api.Features.Settings.ICheckoutSettingsService, ecomm.api.Features.Settings.CheckoutSettingsService>();
builder.Services.AddScoped<ecomm.api.Features.Dashboard.IDashboardService, ecomm.api.Features.Dashboard.DashboardService>();
builder.Services.AddScoped<ecomm.api.Features.Customers.ICustomerAdminService, ecomm.api.Features.Customers.CustomerAdminService>();
builder.Services.AddScoped<ecomm.api.Features.Collections.ICollectionService, ecomm.api.Features.Collections.CollectionService>();
builder.Services.AddScoped<ecomm.api.Features.Navigation.INavigationService, ecomm.api.Features.Navigation.NavigationService>();
builder.Services.AddScoped<ecomm.api.Features.Policies.IPolicyService, ecomm.api.Features.Policies.PolicyService>();
builder.Services.AddScoped<ecomm.api.Features.Storefront.IStorefrontPreferencesService, ecomm.api.Features.Storefront.StorefrontPreferencesService>();
builder.Services.AddScoped<ecomm.api.Features.Storefront.IStorefrontThemeService, ecomm.api.Features.Storefront.StorefrontThemeService>();
builder.Services.AddScoped<ecomm.api.Features.Storefront.IThemeAuthoringService, ecomm.api.Features.Storefront.ThemeAuthoringService>();
builder.Services.AddScoped<ecomm.api.Features.Storefront.IThemeLibraryService, ecomm.api.Features.Storefront.ThemeLibraryService>();
builder.Services.AddScoped<ecomm.api.Features.Staff.IStaffAdminService, ecomm.api.Features.Staff.StaffAdminService>();
builder.Services.AddScoped<ecomm.api.Features.Payments.IPaymentSettingsService, ecomm.api.Features.Payments.PaymentSettingsService>();
builder.Services.AddScoped<ecomm.api.Features.Shipping.IShippingAdminService, ecomm.api.Features.Shipping.ShippingAdminService>();
builder.Services.AddScoped<ecomm.api.Features.Shipping.Shiprocket.IShiprocketSettingsService, ecomm.api.Features.Shipping.Shiprocket.ShiprocketSettingsService>();
builder.Services.AddScoped<ecomm.api.Features.Shipping.Shiprocket.ITenantShiprocketService, ecomm.api.Features.Shipping.Shiprocket.TenantShiprocketService>();
builder.Services.AddScoped<ecomm.api.Features.Shipping.Shiprocket.IShiprocketWebhookService, ecomm.api.Features.Shipping.Shiprocket.ShiprocketWebhookService>();
// Shiprocket courier rates — config-gated (mirrors the SMS/Razorpay pattern). Disabled by default.
builder.Services.Configure<ecomm.api.Features.Shipping.Shiprocket.ShiprocketOptions>(
    builder.Configuration.GetSection(ecomm.api.Features.Shipping.Shiprocket.ShiprocketOptions.SectionName));
if ((builder.Configuration["Shiprocket:Provider"] ?? "None").Equals("Shiprocket", StringComparison.OrdinalIgnoreCase))
    builder.Services.AddSingleton<ecomm.api.Features.Shipping.Shiprocket.IShiprocketClient>(sp =>
        new ecomm.api.Features.Shipping.Shiprocket.ShiprocketClient(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient("shiprocket"),
            sp.GetRequiredService<IOptions<ecomm.api.Features.Shipping.Shiprocket.ShiprocketOptions>>(),
            sp.GetRequiredService<ILogger<ecomm.api.Features.Shipping.Shiprocket.ShiprocketClient>>()));
else
    builder.Services.AddSingleton<ecomm.api.Features.Shipping.Shiprocket.IShiprocketClient,
        ecomm.api.Features.Shipping.Shiprocket.NullShiprocketClient>();
// AI (V2 AI-0): provider-agnostic text generation, config-gated (Ai:Provider None|OpenAI — mirrors the
// SMS/Shiprocket pattern), plus the credit ledger + metering wrapper. Disabled by default (None → NullAiService).
builder.Services.Configure<ecomm.api.Features.Ai.AiOptions>(builder.Configuration.GetSection(ecomm.api.Features.Ai.AiOptions.SectionName));
// Image generation reuses the text key unless its own is set, so enabling images is just Ai:ImageProvider=OpenAI.
builder.Services.PostConfigure<ecomm.api.Features.Ai.AiOptions>(o =>
{
    if (string.IsNullOrWhiteSpace(o.Image.ApiKey)) o.Image.ApiKey = o.OpenAi.ApiKey;
});
if ((builder.Configuration["Ai:Provider"] ?? "None").Equals("OpenAI", StringComparison.OrdinalIgnoreCase))
    builder.Services.AddScoped<ecomm.api.Features.Ai.IAiService>(sp => new ecomm.api.Features.Ai.OpenAiService(
        sp.GetRequiredService<IHttpClientFactory>().CreateClient("openai"),
        sp.GetRequiredService<IOptions<ecomm.api.Features.Ai.AiOptions>>(),
        sp.GetRequiredService<ILogger<ecomm.api.Features.Ai.OpenAiService>>()));
else
    builder.Services.AddScoped<ecomm.api.Features.Ai.IAiService, ecomm.api.Features.Ai.NullAiService>();
// Image provider — config-gated the same way (Ai:ImageProvider None|OpenAI); Gemini/Imagen slots in later.
if ((builder.Configuration["Ai:ImageProvider"] ?? "None").Equals("OpenAI", StringComparison.OrdinalIgnoreCase))
    builder.Services.AddScoped<ecomm.api.Features.Ai.IImageAiService>(sp => new ecomm.api.Features.Ai.OpenAiImageService(
        sp.GetRequiredService<IHttpClientFactory>().CreateClient("openai-image"),
        sp.GetRequiredService<IOptions<ecomm.api.Features.Ai.AiOptions>>(),
        sp.GetRequiredService<ILogger<ecomm.api.Features.Ai.OpenAiImageService>>()));
else
    builder.Services.AddScoped<ecomm.api.Features.Ai.IImageAiService, ecomm.api.Features.Ai.NullImageAiService>();
builder.Services.AddScoped<ecomm.api.Features.Ai.IAiCreditService, ecomm.api.Features.Ai.AiCreditService>();
builder.Services.AddScoped<ecomm.api.Features.Ai.IAiImproveService, ecomm.api.Features.Ai.AiImproveService>();
builder.Services.AddScoped<ecomm.api.Features.Ai.IAiCatalogService, ecomm.api.Features.Ai.AiCatalogService>();
builder.Services.AddScoped<ecomm.api.Features.Ai.IAiImportService, ecomm.api.Features.Ai.AiImportService>();
builder.Services.AddScoped<ecomm.api.Features.Ai.IAiPageService, ecomm.api.Features.Ai.AiPageService>();
// Platform-side gateway (from the app-wide Payments config) — merchant→platform payments (AI credit top-ups).
// Scoped, not singleton: it reads PlatformPaymentSettings (console-set keys override api.env).
builder.Services.AddScoped<ecomm.api.Features.Payments.PlatformPaymentGatewayFactory>();

// Encrypts per-tenant secrets at rest (Razorpay key secret, Shiprocket password). The key ring MUST
// survive restarts or every saved secret becomes undecryptable — persist it to disk. Path is
// configurable (DataProtection:KeysPath); default lives under the content root, which `dotnet publish`
// leaves untouched (it overwrites files but never deletes unknown directories).
var dpKeysPath = builder.Configuration["DataProtection:KeysPath"];
if (string.IsNullOrWhiteSpace(dpKeysPath))
    dpKeysPath = Path.Combine(builder.Environment.ContentRootPath, "dp-keys");
Directory.CreateDirectory(dpKeysPath);
builder.Services.AddDataProtection()
    .SetApplicationName("wavcommerce")
    .PersistKeysToFileSystem(new DirectoryInfo(dpKeysPath));
// Tenant-aware payment gateway: prefer the current tenant's own Razorpay config
// (TenantPaymentAccounts, secret decrypted), else fall back to the app-wide Payments config, else Mock.
builder.Services.AddScoped<IPaymentGateway>(sp =>
{
    var httpFactory = sp.GetRequiredService<IHttpClientFactory>();
    var db = sp.GetRequiredService<ecomm.api.Data.Context.EcommerceDbContext>();
    var acct = db.TenantPaymentAccounts.AsNoTracking().FirstOrDefault();   // tenant-scoped by global filter
    if (acct is { IsEnabled: true } && acct.Provider.Equals("Razorpay", StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(acct.RazorpayKeyId) && !string.IsNullOrEmpty(acct.RazorpayKeySecret))
    {
        try
        {
            var protector = sp.GetRequiredService<IDataProtectionProvider>()
                .CreateProtector(ecomm.api.Features.Payments.PaymentSettingsService.ProtectorPurpose);
            var secret = protector.Unprotect(acct.RazorpayKeySecret!);
            return new RazorpayPaymentGateway(httpFactory.CreateClient("razorpay"), acct.RazorpayKeyId!, secret);
        }
        catch { /* corrupt/rotated key material → fall through to app default */ }
    }

    var opt = sp.GetRequiredService<IOptions<PaymentOptions>>().Value;
    if (opt.Provider.Equals("Razorpay", StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(opt.RazorpayKeyId) && !string.IsNullOrWhiteSpace(opt.RazorpayKeySecret))
        return new RazorpayPaymentGateway(httpFactory.CreateClient("razorpay"), opt.RazorpayKeyId!, opt.RazorpayKeySecret!);

    return new MockPaymentGateway();
});

// JWT bearer authentication
var jwt = builder.Configuration.GetSection("Jwt").Get<JwtSettings>() ?? new JwtSettings();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
        };
        // SignalR (WebSocket) can't send an Authorization header — read the token from the query string.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken) && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                    context.Token = accessToken;
                return Task.CompletedTask;
            },
        };
    });
builder.Services.AddAuthorization();

// Trust the reverse proxy (Nginx on the same host) so the API sees the real client IP + scheme.
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownNetworks.Clear();
    o.KnownProxies.Clear();
});

// Output caching for anonymous storefront reads (authenticated requests bypass automatically).
// Output caching registration is kept so the [OutputCache(PolicyName="public")] attributes
// still resolve, BUT the middleware is DISABLED below (see app.UseOutputCache).
// Reason: the in-memory OutputCache does not reliably vary its key by tenant here, so a
// cached anonymous storefront page could leak across stores. Correctness > the perf win.
// V2-7 re-introduces caching properly as tenant-namespaced Redis (design-v2.md §6.2 / V2-7).
builder.Services.AddOutputCache(options =>
{
    options.AddPolicy("public", b => b.Expire(TimeSpan.FromSeconds(60)).SetVaryByQuery("*"));
});

// Readiness health check (DB probe) + response compression.
builder.Services.AddHealthChecks().AddCheck<ecomm.api.Common.Health.DatabaseHealthCheck>("database");
builder.Services.AddResponseCompression(o =>
{
    o.EnableForHttps = true;
    o.Providers.Add<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProvider>();
    o.Providers.Add<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProvider>();
});

// Rate limiting — throttle sensitive auth endpoints per client IP (brute-force / OTP abuse).
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", ctx => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    // Contact form: anonymous writes, so keep it tight — a human sends one message, not five an hour.
    options.AddPolicy("contact", ctx => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromHours(1), QueueLimit = 0 }));
    // Guest order lookup: order numbers are enumerable, so this is the realistic brute-force
    // target. Generous enough for a shopper who mistypes their email twice, useless for a scan.
    options.AddPolicy("lookup", ctx => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(10), QueueLimit = 0 }));
});

var app = builder.Build();

// --- Pipeline -------------------------------------------------------------
app.UseForwardedHeaders();   // real client IP + original scheme (must be first, behind Nginx)
app.UseResponseCompression();

// Security headers on every response.
app.Use(async (ctx, next) =>
{
    var h = ctx.Response.Headers;
    h["X-Content-Type-Options"] = "nosniff";
    h["X-Frame-Options"] = "DENY";
    h["Referrer-Policy"] = "strict-origin-when-cross-origin";
    h["X-Permitted-Cross-Domain-Policies"] = "none";
    await next();
});

app.UseMiddleware<CorrelationIdMiddleware>();   // trace id on every request + log line
app.UseSerilogRequestLogging();
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    app.UseHsts();
}

app.UseHttpsRedirection();

// Serve uploaded media from the configured folder at Media:RequestPath.
// Dev relies on this; prod fronts it with an Nginx `location /uploads/` for speed.
var mediaOpts = app.Configuration.GetSection(ecomm.api.Features.Media.MediaOptions.SectionName)
    .Get<ecomm.api.Features.Media.MediaOptions>() ?? new ecomm.api.Features.Media.MediaOptions();
var mediaRoot = Path.IsPathRooted(mediaOpts.UploadPath)
    ? mediaOpts.UploadPath
    : Path.Combine(app.Environment.ContentRootPath, mediaOpts.UploadPath);
Directory.CreateDirectory(mediaRoot);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(mediaRoot),
    RequestPath = mediaOpts.RequestPath,
});

app.UseCors(AngularCors);
// app.UseOutputCache();  // DISABLED for multi-tenant safety — re-enable with tenant-namespaced Redis in V2-7.
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<TenantResolutionMiddleware>();   // resolve store from subdomain (apex → default tenant)
app.UseMiddleware<ImpersonationGuardMiddleware>(); // block writes during read-only impersonation
app.UseMiddleware<StaffAccessGuardMiddleware>();   // enforce staff access levels (Viewer read-only, Disabled blocked)
app.MapControllers();
app.MapHealthChecks("/api/health/ready");   // 200 Healthy / 503 if DB unreachable
app.MapHub<ecomm.api.Features.Notifications.NotificationHub>("/hubs/notifications");
app.MapHangfireDashboard("/admin/jobs", new DashboardOptions
{
    Authorization = new[] { new ecomm.api.Common.Middleware.HangfireAuthorizationFilter() },
});

// Subscription lifecycle sweep (v4 Phase 0): replaces the old BackgroundService timer. Registering the
// recurring job on every startup keeps its cron/target in sync with this code; the job method itself
// (RunScheduledLifecycleSweepAsync) computes "now" fresh at each actual execution, not at this
// registration call — see the interface doc comment on why that distinction matters for Hangfire.
// 1-23, not 1-24 — "*/24" is not a valid cron hours step (hours only range 0-23). Dormant today
// since the default (6) never reaches the boundary, but a real latent bug — caught live when the
// same one-line pattern, copied for Dynamic Pricing's own sweep below, crashed startup at its
// default of 24.
var sweepIntervalHours = Math.Clamp(builder.Configuration.GetValue("Billing:SweepIntervalHours", 6), 1, 23);
RecurringJob.AddOrUpdate<ecomm.api.Features.Subscriptions.ISubscriptionService>(
    "subscription-lifecycle-sweep",
    svc => svc.RunScheduledLifecycleSweepAsync(CancellationToken.None),
    $"0 */{sweepIntervalHours} * * *");

// Dynamic Pricing suggestion generation (v4 Phase 5) — once daily by default, loops every
// entitled tenant in its own scope (RunScheduledGenerationAsync). Approval-mode only; this job
// only ever creates Pending suggestions, never changes a price itself.
// 1-23, not 1-24 — "*/24" is not a valid cron hours step (hours only range 0-23), which crashed
// startup in production the first time this shipped (default 24 → invalid "0 */24 * * *").
var pricingIntervalHours = Math.Clamp(builder.Configuration.GetValue("Pricing:SweepIntervalHours", 24), 1, 23);
RecurringJob.AddOrUpdate<ecomm.api.Features.Pricing.IPricingEngineService>(
    "dynamic-pricing-generation",
    svc => svc.RunScheduledGenerationAsync(CancellationToken.None),
    $"0 */{pricingIntervalHours} * * *");

app.Run();
