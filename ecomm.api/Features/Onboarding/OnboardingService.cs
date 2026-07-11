using System.Text.RegularExpressions;
using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Auth.Dtos;
using ecomm.api.Features.Auth.Services;
using ecomm.api.Features.Storefront;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ecomm.api.Features.Onboarding;

public sealed record SignupRequest(
    string StoreName, string Slug, string OwnerName, string OwnerEmail, string Password, string? PlanSlug);

public sealed record OnboardingResult(
    long TenantId, string Slug, string StoreUrl, string AccessToken, DateTime TrialEndsAt);

public interface IOnboardingService
{
    Task<OnboardingResult> SignupAsync(SignupRequest req, string? ip, CancellationToken ct);
    Task<bool> IsSlugAvailableAsync(string slug, CancellationToken ct);
    /// <summary>A unique, valid store address derived from the name (Shopify-style, e.g. "cafe24-a3k9").</summary>
    Task<string> SuggestSlugAsync(string? storeName, CancellationToken ct);
}

/// <summary>
/// Merchant self-serve signup: creates a Tenant + merchant-admin user + 14-day trial
/// subscription + per-tenant defaults, then returns an auto-login token. All the new
/// rows are written under BeginScope(newTenantId) so they stamp to the new store, not
/// the (apex) tenant the signup request resolved to.
/// </summary>
public sealed partial class OnboardingService(
    EcommerceDbContext db,
    ICurrentTenantService tenant,
    IPasswordHasher hasher,
    IAuthService auth,
    IThemeLibraryService themes,
    ILogger<OnboardingService> logger,
    IOptions<TenancyOptions> tenancy) : IOnboardingService
{
    private const int TrialDays = 14;
    private const string AdminRole = "ADMIN";
    private const string DefaultPlanSlug = "starter";

    private static readonly HashSet<string> ReservedSlugs = new(StringComparer.OrdinalIgnoreCase)
    {
        "www", "api", "admin", "app", "mail", "smtp", "static", "assets", "cdn", "hubs",
        "uploads", "help", "support", "blog", "status", "dashboard", "account", "login",
        "signup", "register", "analytics", "billing", "docs", "store", "shop", "my",
    };

    public async Task<OnboardingResult> SignupAsync(SignupRequest req, string? ip, CancellationToken ct)
    {
        var storeName = (req.StoreName ?? "").Trim();
        var ownerName = (req.OwnerName ?? "").Trim();
        var email = (req.OwnerEmail ?? "").Trim();
        var slug = (req.Slug ?? "").Trim().ToLowerInvariant();

        if (storeName.Length < 2) throw new AppException("Store name is required.", StatusCodes.Status400BadRequest);
        if (!email.Contains('@')) throw new AppException("A valid email is required.", StatusCodes.Status400BadRequest);
        if ((req.Password ?? "").Length < 8) throw new AppException("Password must be at least 8 characters.", StatusCodes.Status400BadRequest);

        // No address chosen → auto-generate a unique Shopify-style handle from the store name.
        if (string.IsNullOrWhiteSpace(slug))
            slug = await GenerateUniqueSlugAsync(storeName, ct);

        if (!SlugPattern().IsMatch(slug))
            throw new AppException("Store address must be 3–40 chars: lowercase letters, numbers, hyphens (not at the ends).", StatusCodes.Status400BadRequest);
        if (ReservedSlugs.Contains(slug))
            throw new AppException("That store address is reserved. Please choose another.", StatusCodes.Status409Conflict);
        if (await db.Tenants.AnyAsync(t => t.Slug == slug, ct))
            throw new AppException("That store address is already taken.", StatusCodes.Status409Conflict);
        if (await db.SignupBlocklist.AnyAsync(b => b.Type == "Email" && b.Value == email.ToLowerInvariant(), ct))
            throw new AppException("Signup isn't available for this account. Contact support.", StatusCodes.Status403Forbidden);

        var plan = await db.Plans.FirstOrDefaultAsync(p => p.Slug == (req.PlanSlug ?? DefaultPlanSlug) && p.IsActive, ct)
                   ?? await db.Plans.FirstOrDefaultAsync(p => p.Slug == DefaultPlanSlug, ct)
                   ?? throw new AppException("No plan available.", StatusCodes.Status500InternalServerError);

        var now = DateTime.UtcNow;
        var trialEnds = now.AddDays(TrialDays);

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // 1) Tenant (not tenant-scoped — set fields directly)
        var newTenant = new Tenant
        {
            Name = storeName, DisplayName = storeName, Code = slug, Slug = slug,
            IsActive = true, TrialEndsAt = trialEnds, PlanId = (int?)plan.PlanId, CreatedAt = now,
        };
        db.Tenants.Add(newTenant);
        await db.SaveChangesAsync(ct);

        AuthResponse tokens;
        using (tenant.BeginScope(newTenant.TenantId))   // everything below stamps to the new store
        {
            var user = new User
            {
                Email = email, NormalizedEmail = email.ToUpperInvariant(),
                PasswordHash = hasher.Hash(req.Password!), FullName = ownerName,
                IsActive = true, IsEmailVerified = true, EmailVerifiedAt = now, CreatedAt = now,
            };
            db.Users.Add(user);
            await db.SaveChangesAsync(ct);

            var adminRole = await db.Roles.FirstOrDefaultAsync(r => r.NormalizedName == AdminRole, ct)
                            ?? throw new AppException("Admin role missing.", StatusCodes.Status500InternalServerError);
            db.UserRoles.Add(new UserRole { UserId = user.UserId, RoleId = adminRole.RoleId });

            db.TenantSubscriptions.Add(new TenantSubscription
            {
                PlanId = plan.PlanId, Status = "Trial",
                CurrentPeriodStart = now, CurrentPeriodEnd = trialEnds, CreatedAt = now,
            });

            // default auth: email/password enabled so the new store can sign in
            db.AuthProviders.Add(new AuthProvider
            {
                Provider = "EmailPassword", IsEnabled = true, AllowRegistration = true,
                DisplayName = "Email & Password", DisplayOrder = 1, CreatedAt = now,
            });

            db.TenantSettings.Add(new TenantSetting { Key = "StoreName", Value = storeName, CreatedAt = now });
            db.TenantSettings.Add(new TenantSetting { Key = "CurrencyCode", Value = "INR", CreatedAt = now });

            // Default shipping so the store can check out on day one (editable in /admin/shipping).
            db.ShippingMethods.Add(new ShippingMethod
            {
                Name = "Standard Delivery", Description = "Flat-rate delivery — free over ₹499",
                RateType = "Flat", BaseRate = 49m, FreeShippingThreshold = 499m, EstimatedDays = 5,
                IsActive = true, CreatedAt = now,
            });
            // Cash on Delivery on by default so orders can be placed before Razorpay is configured.
            db.Settings.Add(new Setting { SettingKey = "CodEnabled", SettingValue = "true", DataType = "string", Category = "Billing", CreatedAt = now });

            await db.SaveChangesAsync(ct);

            tokens = await auth.IssueTokensForUserAsync(user, ip, ct);
        }

        await tx.CommitAsync(ct);

        // Best-effort: publish a clean starter theme so the storefront renders immediately.
        // Non-fatal — on failure the store keeps the default chrome and the merchant picks a theme.
        try
        {
            using (tenant.BeginScope(newTenant.TenantId))
            {
                var starter = await themes.InstallPrebuiltAsync("minimal", ct);
                await themes.PublishAsync(starter.ThemeId, ct);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Signup: starter-theme provisioning failed for tenant {TenantId} (non-fatal).", newTenant.TenantId);
        }

        return new OnboardingResult(newTenant.TenantId, slug, BuildStoreUrl(slug), tokens.AccessToken, trialEnds);
    }

    public async Task<bool> IsSlugAvailableAsync(string slug, CancellationToken ct)
    {
        slug = (slug ?? "").Trim().ToLowerInvariant();
        if (!SlugPattern().IsMatch(slug) || ReservedSlugs.Contains(slug)) return false;
        return !await db.Tenants.AnyAsync(t => t.Slug == slug, ct);
    }

    public Task<string> SuggestSlugAsync(string? storeName, CancellationToken ct) => GenerateUniqueSlugAsync(storeName, ct);

    /// <summary>
    /// A unique, valid store address derived from the name plus a short random suffix (Shopify-style:
    /// "cafe24-a3k9"). The suffix guarantees uniqueness at scale and stops a one-word name from being a
    /// squattable single-letter/word subdomain. Falls back to "store-xxxxxx" when the name yields nothing.
    /// </summary>
    private async Task<string> GenerateUniqueSlugAsync(string? storeName, CancellationToken ct)
    {
        var baseSlug = Slugify(storeName);
        if (baseSlug.Length < 3) baseSlug = "store";

        for (var attempt = 0; attempt < 50; attempt++)
        {
            var candidate = $"{baseSlug}-{RandomToken(attempt < 25 ? 4 : 6)}";
            if (candidate.Length <= 40 && SlugPattern().IsMatch(candidate)
                && !ReservedSlugs.Contains(candidate) && !await db.Tenants.AnyAsync(t => t.Slug == candidate, ct))
                return candidate;
        }
        return $"store-{RandomToken(8)}";
    }

    /// <summary>Lowercase, hyphenate and trim a name into a slug base (max 24 chars, no leading/trailing hyphen).</summary>
    private static string Slugify(string? name)
    {
        var s = SlugStripRegex().Replace((name ?? "").ToLowerInvariant(), "-").Trim('-');
        if (s.Length > 24) s = s[..24].Trim('-');
        return s;
    }

    /// <summary>A base-36 token (0-9a-z) of the given length, from crypto-strong randomness.</summary>
    private static string RandomToken(int length)
    {
        const string alphabet = "0123456789abcdefghijklmnopqrstuvwxyz";
        var bytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(length);
        return string.Create(length, bytes, (span, b) => { for (var i = 0; i < span.Length; i++) span[i] = alphabet[b[i] % alphabet.Length]; });
    }

    private string BuildStoreUrl(string slug)
    {
        var baseDomain = tenancy.Value.BaseDomain;
        return string.IsNullOrEmpty(baseDomain)
            ? $"http://{slug}.localhost:4200"                 // dev — configure Tenancy:BaseDomain for prod
            : $"https://{slug}.{baseDomain}";
    }

    // 3–40 chars: lowercase letters/numbers/hyphens, not at the ends. (The optional group is now
    // required, so 1–2 char slugs like "c" are rejected — matching the error message.)
    [GeneratedRegex("^[a-z0-9][a-z0-9-]{1,38}[a-z0-9]$")]
    private static partial Regex SlugPattern();

    /// <summary>Anything that isn't a lowercase letter or digit → a hyphen (collapsed).</summary>
    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex SlugStripRegex();
}
