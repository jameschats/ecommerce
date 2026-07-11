using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ecomm.api.Common.Exceptions;
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
public sealed record ShiprocketAddress(
    string Name, string? Phone, string? Email, string Line1, string? Line2, string City, string State, string Pincode, string? Country);

public sealed record ShiprocketItem(string Name, string? Sku, int Units, decimal SellingPrice);

public sealed record ShiprocketOrderInput(
    string OrderNumber, DateTime OrderDateUtc, string PaymentMethod, decimal SubTotal,
    ShiprocketAddress ShipTo, IReadOnlyList<ShiprocketItem> Items, decimal WeightKg);

/// <summary>Outcome of pushing an order to Shiprocket: their ids + (best-effort) the assigned AWB/courier.</summary>
public sealed record ShiprocketShipResult(string ProviderOrderId, string ProviderShipmentId, string? Awb, string? CourierName);

public interface ITenantShiprocketService
{
    /// <summary>True when this store has Shiprocket selected as its fulfillment method.</summary>
    Task<bool> IsEnabledAsync(CancellationToken ct = default);
    Task<ShiprocketRate?> GetCheapestRateAsync(string deliveryPincode, decimal weightKg, bool cod, CancellationToken ct = default);
    /// <summary>Create the order in Shiprocket and assign the cheapest courier's AWB. Throws on failure (SR3).</summary>
    Task<ShiprocketShipResult> ShipAsync(ShiprocketOrderInput input, CancellationToken ct = default);
    /// <summary>Schedule a pickup for a Shiprocket shipment (SR4). Returns a status/date note.</summary>
    Task<string?> SchedulePickupAsync(string providerShipmentId, CancellationToken ct = default);
    /// <summary>Generate the shipping-label PDF for a Shiprocket shipment (SR4). Returns its URL.</summary>
    Task<string?> GenerateLabelAsync(string providerShipmentId, CancellationToken ct = default);
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

    public async Task<ShiprocketShipResult> ShipAsync(ShiprocketOrderInput input, CancellationToken ct = default)
    {
        var acct = await AccountAsync(ct)
            ?? throw new AppException("Shiprocket is not the active fulfillment method for this store.");
        if (string.IsNullOrWhiteSpace(acct.PickupLocation))
            throw new AppException("Set your Shiprocket pickup location in fulfillment settings before shipping.");
        var token = await GetTokenAsync(acct, ct)
            ?? throw new AppException("Could not authenticate with Shiprocket. Re-check your credentials in settings.");

        var weight = input.WeightKg > 0 ? input.WeightKg : Opt.DefaultWeightKg;
        var orderBody = new
        {
            order_id = input.OrderNumber,
            order_date = input.OrderDateUtc.ToString("yyyy-MM-dd HH:mm"),
            pickup_location = acct.PickupLocation,
            billing_customer_name = input.ShipTo.Name,
            billing_last_name = "",
            billing_address = input.ShipTo.Line1,
            billing_address_2 = input.ShipTo.Line2 ?? "",
            billing_city = input.ShipTo.City,
            billing_pincode = input.ShipTo.Pincode,
            billing_state = input.ShipTo.State,
            billing_country = string.IsNullOrWhiteSpace(input.ShipTo.Country) ? "India" : input.ShipTo.Country,
            billing_email = input.ShipTo.Email ?? "",
            billing_phone = input.ShipTo.Phone ?? "",
            shipping_is_billing = true,
            order_items = input.Items.Select(i => new { name = i.Name, sku = string.IsNullOrWhiteSpace(i.Sku) ? i.Name : i.Sku, units = i.Units, selling_price = i.SellingPrice }).ToArray(),
            payment_method = input.PaymentMethod,
            sub_total = input.SubTotal,
            length = 10, breadth = 10, height = 5, weight,
        };

        using var orderDoc = await PostAsync("v1/external/orders/create/adhoc", orderBody, token, ct);
        var root = orderDoc.RootElement;
        var srOrderId = GetString(root, "order_id");
        var shipmentId = GetString(root, "shipment_id");
        if (string.IsNullOrEmpty(shipmentId))
            throw new AppException($"Shiprocket did not create a shipment. {GetString(root, "message") ?? "Check the order address/pincode."}", 502);

        // Assign the cheapest courier's AWB — best-effort: if it fails the order still exists in Shiprocket,
        // and the merchant can assign a courier from the Shiprocket panel or retry.
        string? awb = null, courier = null;
        try
        {
            using var awbDoc = await PostAsync("v1/external/courier/assign/awb", new { shipment_id = shipmentId }, token, ct);
            if (awbDoc.RootElement.TryGetProperty("response", out var resp) && resp.TryGetProperty("data", out var data))
            {
                awb = GetString(data, "awb_code");
                courier = GetString(data, "courier_name");
            }
        }
        catch (Exception ex) { log.LogWarning(ex, "Shiprocket AWB assignment failed for order {Order} (shipment {Shipment} created).", input.OrderNumber, shipmentId); }

        return new ShiprocketShipResult(srOrderId ?? "", shipmentId, awb, courier);
    }

    public async Task<string?> SchedulePickupAsync(string providerShipmentId, CancellationToken ct = default)
    {
        var token = await RequireTokenAsync(ct);
        using var doc = await PostAsync("v1/external/courier/generate/pickup", new { shipment_id = new[] { providerShipmentId } }, token, ct);
        var root = doc.RootElement;
        // Shiprocket returns pickup_scheduled_date (at root or under "response"); fall back to a generic note.
        return GetString(root, "pickup_scheduled_date")
            ?? (root.TryGetProperty("response", out var r) && r.ValueKind == JsonValueKind.Object ? GetString(r, "pickup_scheduled_date") : null)
            ?? "Pickup requested";
    }

    public async Task<string?> GenerateLabelAsync(string providerShipmentId, CancellationToken ct = default)
    {
        var token = await RequireTokenAsync(ct);
        using var doc = await PostAsync("v1/external/courier/generate/label", new { shipment_id = new[] { providerShipmentId } }, token, ct);
        return GetString(doc.RootElement, "label_url");
    }

    private async Task<string> RequireTokenAsync(CancellationToken ct)
    {
        var acct = await AccountAsync(ct)
            ?? throw new AppException("Shiprocket is not the active fulfillment method for this store.");
        return await GetTokenAsync(acct, ct)
            ?? throw new AppException("Could not authenticate with Shiprocket. Re-check your credentials in settings.");
    }

    /// <summary>POST JSON with the tenant's bearer token; throws <see cref="AppException"/> on a non-2xx.</summary>
    private async Task<JsonDocument> PostAsync(string path, object body, string token, CancellationToken ct)
    {
        var http = httpFactory.CreateClient("shiprocket");
        http.BaseAddress ??= new Uri(Opt.BaseUrl);
        using var req = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var res = await http.SendAsync(req, ct);
        var json = await res.Content.ReadAsStringAsync(ct);
        if (res.StatusCode == HttpStatusCode.Unauthorized) cache.Remove(TokenKey);
        if (!res.IsSuccessStatusCode)
            throw new AppException($"Shiprocket request failed ({(int)res.StatusCode}): {Truncate(json)}", 502);
        return JsonDocument.Parse(json);
    }

    /// <summary>Read a property as string whether Shiprocket returns it as a JSON number or string.</summary>
    private static string? GetString(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var p)
            ? p.ValueKind switch { JsonValueKind.String => p.GetString(), JsonValueKind.Number => p.ToString(), _ => null }
            : null;

    private static string Truncate(string s) => s.Length <= 300 ? s : s[..300];
}
