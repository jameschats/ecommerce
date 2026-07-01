using System.Text;
using ecomm.api.Common.Middleware;
using ecomm.api.Data.Context;
using ecomm.api.Features.Auth;
using ecomm.api.Features.Auth.Services;
using ecomm.api.Features.Payments;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
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
    });
builder.Services.AddAuthorization();

var app = builder.Build();

// --- Pipeline -------------------------------------------------------------
app.UseSerilogRequestLogging();
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors(AngularCors);
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
