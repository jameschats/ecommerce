using System.Text;
using System.Threading.RateLimiting;
using ecomm.api.Common.Middleware;
using Microsoft.AspNetCore.HttpOverrides;
using ecomm.api.Data.Context;
using ecomm.api.Features.Auth;
using ecomm.api.Features.Auth.Services;
using ecomm.api.Features.Payments;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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

// CORS for the Angular dev server (ecomm.web)
const string AngularCors = "AngularCors";
var angularOrigin = builder.Configuration["Cors:AngularOrigin"] ?? "http://localhost:4200";
builder.Services.AddCors(options =>
    options.AddPolicy(AngularCors, policy =>
        policy.WithOrigins(angularOrigin).AllowAnyHeader().AllowAnyMethod()));

// EF Core (MySQL, database-first via Pomelo)
var connectionString = builder.Configuration.GetConnectionString("Default");
builder.Services.AddDbContext<EcommerceDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

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
builder.Services.AddScoped<ecomm.api.Features.Cms.IGalleryService, ecomm.api.Features.Cms.GalleryService>();
builder.Services.Configure<ecomm.api.Features.Media.MediaOptions>(builder.Configuration.GetSection(ecomm.api.Features.Media.MediaOptions.SectionName));
builder.Services.AddSingleton<ecomm.api.Features.Media.IMediaStorage, ecomm.api.Features.Media.LocalDiskStorage>();
builder.Services.AddScoped<ecomm.api.Features.Media.IMediaService, ecomm.api.Features.Media.MediaService>();

// Notifications — the sender resolves Mock/Live and its SMTP settings from the database on
// every send (design.md §9.2), so an admin can switch it from a screen without a redeploy.
// The old startup-time Email:Provider branch is gone; EmailOptions stays bound for the
// legacy SmtpEmailSender, which remains in the codebase unused.
builder.Services.Configure<ecomm.api.Features.Notifications.EmailOptions>(builder.Configuration.GetSection(ecomm.api.Features.Notifications.EmailOptions.SectionName));
builder.Services.AddScoped<ecomm.api.Features.Notifications.ConfiguredEmailSender>();
builder.Services.AddScoped<ecomm.api.Features.Notifications.IEmailSender>(
    sp => sp.GetRequiredService<ecomm.api.Features.Notifications.ConfiguredEmailSender>());
builder.Services.AddScoped<ecomm.api.Features.Notifications.INotificationService, ecomm.api.Features.Notifications.NotificationService>();
builder.Services.AddSignalR();
builder.Services.AddScoped<ecomm.api.Features.Notifications.INotificationFeedService, ecomm.api.Features.Notifications.NotificationFeedService>();
builder.Services.AddScoped<ecomm.api.Features.Reviews.IReviewService, ecomm.api.Features.Reviews.ReviewService>();
builder.Services.AddScoped<ecomm.api.Features.Coupons.ICouponService, ecomm.api.Features.Coupons.CouponService>();
builder.Services.AddScoped<ecomm.api.Features.Wishlist.IWishlistService, ecomm.api.Features.Wishlist.WishlistService>();
builder.Services.AddScoped<ecomm.api.Features.Analytics.IAnalyticsService, ecomm.api.Features.Analytics.AnalyticsService>();
builder.Services.AddScoped<ecomm.api.Features.Suppliers.ISupplierService, ecomm.api.Features.Suppliers.SupplierService>();

// Inventory & Search
builder.Services.AddScoped<ecomm.api.Features.Inventory.IInventoryService, ecomm.api.Features.Inventory.InventoryService>();
builder.Services.AddScoped<ecomm.api.Features.Inventory.IInventoryImportService, ecomm.api.Features.Inventory.InventoryImportService>();
builder.Services.AddScoped<ecomm.api.Features.Search.ISearchService, ecomm.api.Features.Search.SearchService>();

// Shopping (Stage 4)
builder.Services.AddScoped<ecomm.api.Features.Account.IAccountService, ecomm.api.Features.Account.AccountService>();
builder.Services.AddScoped<ecomm.api.Features.Cart.ICartService, ecomm.api.Features.Cart.CartService>();

// Checkout & Money (Stage 5)
builder.Services.Configure<PaymentOptions>(builder.Configuration.GetSection(PaymentOptions.SectionName));
builder.Services.AddHttpClient();
builder.Services.AddScoped<ecomm.api.Features.Checkout.ITaxService, ecomm.api.Features.Checkout.TaxService>();
builder.Services.AddScoped<ecomm.api.Features.Checkout.IShippingService, ecomm.api.Features.Checkout.ShippingService>();
builder.Services.AddScoped<ecomm.api.Features.Checkout.IQuickOrderService, ecomm.api.Features.Checkout.QuickOrderService>();
builder.Services.AddScoped<ecomm.api.Features.Payments.IManualPaymentService, ecomm.api.Features.Payments.ManualPaymentService>();
builder.Services.AddScoped<ecomm.api.Features.Notifications.IOrderMailer, ecomm.api.Features.Notifications.OrderMailer>();
builder.Services.AddScoped<ecomm.api.Features.Catalog.Services.IProductImageZipService, ecomm.api.Features.Catalog.Services.ProductImageZipService>();
builder.Services.AddScoped<ecomm.api.Features.Orders.IInvoiceService, ecomm.api.Features.Orders.InvoiceService>();
builder.Services.AddScoped<ecomm.api.Features.Orders.IOrderService, ecomm.api.Features.Orders.OrderService>();
builder.Services.AddScoped<ecomm.api.Features.Settings.IStoreSettingsService, ecomm.api.Features.Settings.StoreSettingsService>();
builder.Services.AddScoped<IPaymentGateway>(sp =>
{
    var opt = sp.GetRequiredService<IOptions<PaymentOptions>>().Value;
    if (opt.Provider.Equals("Razorpay", StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(opt.RazorpayKeyId) && !string.IsNullOrWhiteSpace(opt.RazorpayKeySecret))
    {
        var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient("razorpay");
        return new RazorpayPaymentGateway(http, opt.RazorpayKeyId!, opt.RazorpayKeySecret!);
    }
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
app.UseOutputCache();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/api/health/ready");   // 200 Healthy / 503 if DB unreachable
app.MapHub<ecomm.api.Features.Notifications.NotificationHub>("/hubs/notifications");

app.Run();
