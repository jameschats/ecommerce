using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace ecomm.api.Features.Shipping.Shiprocket;

/// <summary>
/// Per-tenant Shiprocket client (scoped). Uses the current tenant's connected account
/// (<see cref="TenantShippingAccount"/> with IsEnabled) — its own credentials, pickup pincode and a
/// bearer token cached in-memory per tenant (~9 days). All calls are best-effort: any failure returns
/// null/empty so the caller falls back to the store's manual (Self) shipping. Rates live here (SR2);
/// create-shipment / AWB / pickup / label / tracking are added in SR3–SR5.
/// </summary>
public interface ITenantShiprocketService
{
    /// <summary>True when this store has Shiprocket selected as its fulfillment method.</summary>
    Task<bool> IsEnabledAsync(CancellationToken ct = default);
    Task<ShiprocketRate?> GetCheapestRateAsync(string deliveryPincode, decimal weightKg, bool cod, CancellationToken ct = default);
}

public sealed class TenantShiprocketService(
    EcommerceDbContext db, IDataProtectionProvider dp, IHttpClientFactory httpFactory,
    IMemoryCache cache, IOptions<ShiprocketOptions> opt, ILogger<TenantShiprocketService> log)
    : ITenantShiprocketService
{
    private ShiprocketOptions Opt => opt.Value;
    private IDataProtector Protector => dp.CreateProtector(ShiprocketSettingsService.ProtectorPurpose);
    private long Tenant => db.CurrentTenantId;
    private string TokenKey => $"shiprocket:token:{Tenant}";

    /// <summary>The store's active Shiprocket account (only when it has selected Shiprocket).</summary>
    private Task<TenantShippingAccount?> AccountAsync(CancellationToken ct) =>
        db.TenantShippingAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.IsEnabled, ct);

    public Task<bool> IsEnabledAsync(CancellationToken ct = default) =>
        db.TenantShippingAccounts.AsNoTracking().AnyAsync(a => a.IsEnabled, ct);

    public async Task<ShiprocketRate?> GetCheapestRateAsync(string deliveryPincode, decimal weightKg, bool cod, CancellationToken ct = default)
    {
        var acct = await AccountAsync(ct);
        if (acct is null || string.IsNullOrWhiteSpace(acct.PickupPincode) || string.IsNullOrWhiteSpace(deliveryPincode))
            return null;

        var token = await GetTokenAsync(acct, ct);
        if (token is null) return null;

        var weight = weightKg > 0 ? weightKg : Opt.DefaultWeightKg;
        try
        {
            var http = httpFactory.CreateClient("shiprocket");
            http.BaseAddress ??= new Uri(Opt.BaseUrl);
            var url = $"v1/external/courier/serviceability/?pickup_postcode={acct.PickupPincode}"
                    + $"&delivery_postcode={deliveryPincode}&weight={weight}&cod={(cod ? 1 : 0)}";
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var res = await http.SendAsync(req, ct);
            if (!res.IsSuccessStatusCode)
            {
                if (res.StatusCode == HttpStatusCode.Unauthorized) cache.Remove(TokenKey);   // force re-auth next time
                log.LogWarning("Shiprocket serviceability {Status} for tenant {Tenant}", (int)res.StatusCode, Tenant);
                return null;
            }
            return ShiprocketClient.ParseCheapest(await res.Content.ReadAsStringAsync(ct));
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Shiprocket rate lookup failed for tenant {Tenant} — falling back to manual.", Tenant);
            return null;
        }
    }

    /// <summary>Get a bearer token for this tenant's account, cached in-memory (~9 days).</summary>
    private async Task<string?> GetTokenAsync(TenantShippingAccount acct, CancellationToken ct)
    {
        if (cache.TryGetValue(TokenKey, out string? cached) && !string.IsNullOrEmpty(cached)) return cached;
        if (string.IsNullOrWhiteSpace(acct.Email) || string.IsNullOrEmpty(acct.PasswordCipher)) return null;

        string password;
        try { password = Protector.Unprotect(acct.PasswordCipher!); }
        catch { log.LogWarning("Shiprocket password could not be decrypted for tenant {Tenant} (key rotated?)", Tenant); return null; }

        try
        {
            var http = httpFactory.CreateClient("shiprocket");
            http.BaseAddress ??= new Uri(Opt.BaseUrl);
            using var res = await http.PostAsJsonAsync("v1/external/auth/login", new { email = acct.Email, password }, ct);
            if (!res.IsSuccessStatusCode) { log.LogWarning("Shiprocket auth failed for tenant {Tenant} ({Status})", Tenant, (int)res.StatusCode); return null; }
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
            var token = doc.RootElement.TryGetProperty("token", out var t) ? t.GetString() : null;
            if (!string.IsNullOrEmpty(token)) cache.Set(TokenKey, token, TimeSpan.FromDays(9));
            return token;
        }
        catch (Exception ex) { log.LogWarning(ex, "Shiprocket auth error for tenant {Tenant}", Tenant); return null; }
    }
}
