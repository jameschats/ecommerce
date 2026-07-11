using System.Net.Http.Json;
using System.Text.Json;
using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ecomm.api.Features.Shipping.Shiprocket;

/// <summary>
/// A store's fulfillment configuration. <c>Method</c> is the two-way choice the merchant makes:
/// <c>Self</c> (manual — enter courier + tracking by hand, use manual shipping rates) or
/// <c>Shiprocket</c> (live rates + auto AWB/pickup/label/tracking). <c>Self</c> is the default.
/// The Shiprocket password is write-only (Data-Protection ciphertext; only <c>HasPassword</c> exposed).
/// </summary>
public sealed record ShiprocketSettingsDto(
    string Method, string? Email, bool HasPassword, string? PickupPincode, string? PickupLocation,
    bool IsVerified, DateTime? ConnectedAt);

public sealed record UpdateShiprocketSettingsRequest(
    string Method, string? Email, string? Password, string? PickupPincode, string? PickupLocation);

public interface IShiprocketSettingsService
{
    Task<ShiprocketSettingsDto> GetAsync(CancellationToken ct = default);
    Task<ShiprocketSettingsDto> UpdateAsync(UpdateShiprocketSettingsRequest req, CancellationToken ct = default);
}

public sealed class ShiprocketSettingsService(
    EcommerceDbContext db, IDataProtectionProvider dp, IHttpClientFactory httpFactory, IOptions<ShiprocketOptions> opt)
    : IShiprocketSettingsService
{
    /// <summary>Data-protection purpose shared with the per-tenant Shiprocket client.</summary>
    public const string ProtectorPurpose = "shipping.shiprocket.password.v1";
    public const string Self = "Self", Shiprocket = "Shiprocket";

    private IDataProtector Protector => dp.CreateProtector(ProtectorPurpose);

    public async Task<ShiprocketSettingsDto> GetAsync(CancellationToken ct = default) =>
        ToDto(await db.TenantShippingAccounts.FirstOrDefaultAsync(ct));

    public async Task<ShiprocketSettingsDto> UpdateAsync(UpdateShiprocketSettingsRequest req, CancellationToken ct = default)
    {
        var method = string.Equals(req.Method, Shiprocket, StringComparison.OrdinalIgnoreCase) ? Shiprocket : Self;

        var acct = await db.TenantShippingAccounts.FirstOrDefaultAsync(ct);
        if (acct is null)
        {
            acct = new TenantShippingAccount { Provider = Shiprocket, CreatedAt = DateTime.UtcNow };
            db.TenantShippingAccounts.Add(acct);
        }

        acct.Email = req.Email?.Trim();
        // Only re-encrypt when a new password is supplied (blank = keep existing).
        if (!string.IsNullOrWhiteSpace(req.Password)) acct.PasswordCipher = Protector.Protect(req.Password.Trim());
        acct.PickupPincode = req.PickupPincode?.Trim();
        acct.PickupLocation = req.PickupLocation?.Trim();

        if (method == Shiprocket)
        {
            if (string.IsNullOrWhiteSpace(acct.Email) || string.IsNullOrEmpty(acct.PasswordCipher))
                throw new AppException("Enter your Shiprocket email and password to use Shiprocket fulfillment.");
            // Verify by authenticating against Shiprocket before switching the store live.
            var verified = await TestAuthAsync(acct.Email!, Protector.Unprotect(acct.PasswordCipher!), ct);
            if (!verified) throw new AppException("Could not sign in to Shiprocket with those credentials. Check the email/password.");
            acct.IsVerified = true;
            acct.IsEnabled = true;
            acct.ConnectedAt ??= DateTime.UtcNow;
        }
        else
        {
            // Self shipping: keep the saved creds but flip Shiprocket off (manual flow takes over).
            acct.IsEnabled = false;
        }

        acct.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return ToDto(acct);
    }

    private async Task<bool> TestAuthAsync(string email, string password, CancellationToken ct)
    {
        try
        {
            var http = httpFactory.CreateClient("shiprocket");
            http.BaseAddress ??= new Uri(opt.Value.BaseUrl);
            using var res = await http.PostAsJsonAsync("v1/external/auth/login", new { email, password }, ct);
            if (!res.IsSuccessStatusCode) return false;
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
            return doc.RootElement.TryGetProperty("token", out var t) && !string.IsNullOrWhiteSpace(t.GetString());
        }
        catch { return false; }
    }

    private static ShiprocketSettingsDto ToDto(TenantShippingAccount? a) => new(
        Method: a?.IsEnabled == true ? Shiprocket : Self,
        Email: a?.Email, HasPassword: !string.IsNullOrEmpty(a?.PasswordCipher),
        PickupPincode: a?.PickupPincode, PickupLocation: a?.PickupLocation,
        IsVerified: a?.IsVerified ?? false, ConnectedAt: a?.ConnectedAt);
}
