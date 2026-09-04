using ecomm.api.Common.Exceptions;
using ecomm.api.Common.Models;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Settings;

public sealed record ShopSettingsDto(
    // Quick order
    decimal MinOrderAmount, decimal PackingChargePct, bool RoundOffEnabled,
    string AnnouncementText, string PriceValidUpto,
    // Manual payment
    string UpiId, string UpiPayeeName,
    string BankAccountName, string BankAccountNumber, string BankIfsc, string BankName,
    // Email (Brevo SMTP)
    string EmailMode, string SmtpHost, int SmtpPort, string SmtpUsername,
    bool SmtpPasswordSet, string FromAddress, string FromName, string AdminNotifyTo,
    // Site identity — browser tab plus the storefront name and header logo
    string BrowserTitle, string FaviconUrl, string SiteName, string SiteNameAccent, string SiteNameSize,
    string LogoUrl, string FooterLogoUrl,
    // How to reach the shop — one source for the contact page, the footer and the
    // storefront's structured data (055). Two mobiles and two landlines: a lot of small
    // Indian shops genuinely run two active mobile numbers (owner + shop floor) plus an
    // old landline still printed on stock, and one "Phone" field couldn't hold that.
    string ContactAddress, string ContactMobile1, string ContactMobile2,
    string ContactLandline1, string ContactLandline2, string ContactEmail, string ContactHours, string ContactCity,
    // Per-state minimum order overrides
    IReadOnlyList<StateMinOrderRow> StateMinOrders);

public sealed record StateMinOrderRow(long? Id, string StateName, decimal MinOrderAmount, bool IsActive);

public sealed record SaveShopSettingsRequest(
    decimal MinOrderAmount, decimal PackingChargePct, bool RoundOffEnabled,
    string? AnnouncementText, string? PriceValidUpto,
    string? UpiId, string? UpiPayeeName,
    string? BankAccountName, string? BankAccountNumber, string? BankIfsc, string? BankName,
    string? EmailMode, string? SmtpHost, int SmtpPort, string? SmtpUsername,
    /// <summary>Null or empty leaves the stored password untouched — the screen never
    /// receives it, so echoing an empty box back would silently wipe it.</summary>
    string? SmtpPassword,
    string? FromAddress, string? FromName, string? AdminNotifyTo,
    string? BrowserTitle, string? FaviconUrl, string? SiteName, string? SiteNameAccent, string? SiteNameSize,
    string? LogoUrl, string? FooterLogoUrl,
    string? ContactAddress, string? ContactMobile1, string? ContactMobile2,
    string? ContactLandline1, string? ContactLandline2, string? ContactEmail, string? ContactHours, string? ContactCity,
    IReadOnlyList<StateMinOrderRow>? StateMinOrders);

public sealed record SendTestEmailRequest(string To);

/// <summary>
/// Shop and payment configuration for the quick-order flow (design.md §10.3).
///
/// These live in the database rather than appsettings.json for one reason: the person who
/// needs to change a UPI ID or a packing charge cannot edit a JSON file on a server.
/// </summary>
[ApiController]
[Route("api/admin/shop-settings")]
[Authorize(Policy = ecomm.api.Common.Security.Perm.SettingsManage)]
public sealed class ShopSettingsController : ControllerBase
{
    private const long Tenant = 1;

    private readonly EcommerceDbContext _db;

    public ShopSettingsController(EcommerceDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var s = await LoadAsync(ct);
        var states = await _db.StateMinOrderAmounts
            .Where(x => x.TenantId == Tenant)
            .OrderBy(x => x.StateName)
            .Select(x => new StateMinOrderRow(x.StateMinOrderAmountId, x.StateName, x.MinOrderAmount, x.IsActive))
            .ToListAsync(ct);

        return Ok(ApiResponse<ShopSettingsDto>.Ok(new ShopSettingsDto(
            Dec(s, "QuickOrder.MinOrderAmount"),
            Dec(s, "QuickOrder.PackingChargePct"),
            Str(s, "QuickOrder.RoundOffEnabled") != "false",
            Str(s, "QuickOrder.AnnouncementText"),
            Str(s, "QuickOrder.PriceValidUpto"),
            Str(s, "Payment.UpiId"),
            Str(s, "Payment.UpiPayeeName"),
            Str(s, "Payment.BankAccountName"),
            Str(s, "Payment.BankAccountNumber"),
            Str(s, "Payment.BankIfsc"),
            Str(s, "Payment.BankName"),
            Str(s, "Channels.EmailMode") is { Length: > 0 } em ? em : "Mock",
            Str(s, "Email.SmtpHost"),
            int.TryParse(Str(s, "Email.SmtpPort"), out var port) ? port : 587,
            Str(s, "Email.SmtpUsername"),
            // Never returned — the screen only needs to know whether one is stored.
            Str(s, "Email.SmtpPassword").Length > 0,
            Str(s, "Email.FromAddress"),
            Str(s, "Email.FromName"),
            Str(s, "Email.AdminNotifyTo"),
            Str(s, "Site.BrowserTitle"),
            Str(s, "Site.FaviconUrl"),
            Str(s, "Site.Name"),
            Str(s, "Site.NameAccent"),
            Str(s, "Site.NameSize"),
            Str(s, "Site.LogoUrl"),
            Str(s, "Site.FooterLogoUrl"),
            Str(s, "Store.AddressLine"),
            Str(s, "Store.Mobile1"),
            Str(s, "Store.Mobile2"),
            Str(s, "Store.Landline1"),
            Str(s, "Store.Landline2"),
            Str(s, "Store.Email"),
            Str(s, "Store.Hours"),
            Str(s, "Store.City"),
            states)));
    }

    [HttpPut]
    public async Task<IActionResult> Save([FromBody] SaveShopSettingsRequest req, CancellationToken ct)
    {
        if (req.PackingChargePct is < 0 or > 100)
            throw new AppException("Packing charge must be between 0 and 100 percent.");
        if (req.MinOrderAmount < 0)
            throw new AppException("Minimum order amount cannot be negative.");

        await SetAsync("QuickOrder.MinOrderAmount", req.MinOrderAmount.ToString("0.##"), ct);
        await SetAsync("QuickOrder.PackingChargePct", req.PackingChargePct.ToString("0.##"), ct);
        await SetAsync("QuickOrder.RoundOffEnabled", req.RoundOffEnabled ? "true" : "false", ct);
        await SetAsync("QuickOrder.AnnouncementText", req.AnnouncementText ?? "", ct);
        await SetAsync("QuickOrder.PriceValidUpto", req.PriceValidUpto ?? "", ct);
        await SetAsync("Payment.UpiId", req.UpiId?.Trim() ?? "", ct);
        await SetAsync("Payment.UpiPayeeName", req.UpiPayeeName?.Trim() ?? "", ct);
        await SetAsync("Payment.BankAccountName", req.BankAccountName?.Trim() ?? "", ct);
        await SetAsync("Payment.BankAccountNumber", req.BankAccountNumber?.Trim() ?? "", ct);
        await SetAsync("Payment.BankIfsc", req.BankIfsc?.Trim().ToUpperInvariant() ?? "", ct);
        await SetAsync("Payment.BankName", req.BankName?.Trim() ?? "", ct);

        await SetAsync("Channels.EmailMode",
            string.Equals(req.EmailMode, "Live", StringComparison.OrdinalIgnoreCase) ? "Live" : "Mock", ct);
        await SetAsync("Email.SmtpHost", req.SmtpHost?.Trim() ?? "", ct);
        await SetAsync("Email.SmtpPort", (req.SmtpPort > 0 ? req.SmtpPort : 587).ToString(), ct);
        await SetAsync("Email.SmtpUsername", req.SmtpUsername?.Trim() ?? "", ct);
        await SetAsync("Email.FromAddress", req.FromAddress?.Trim() ?? "", ct);
        await SetAsync("Email.FromName", req.FromName?.Trim() ?? "", ct);
        await SetAsync("Email.AdminNotifyTo", req.AdminNotifyTo?.Trim() ?? "", ct);

        // Only overwrite the password when one was actually supplied. The GET never returns
        // it, so treating a blank field as "clear it" would wipe the key on every save.
        if (!string.IsNullOrWhiteSpace(req.SmtpPassword))
            await SetAsync("Email.SmtpPassword", req.SmtpPassword.Trim(), ct);

        await SetAsync("Site.BrowserTitle", req.BrowserTitle?.Trim() ?? "", ct);
        await SetAsync("Site.FaviconUrl", req.FaviconUrl?.Trim() ?? "", ct);
        await SetAsync("Site.Name", req.SiteName?.Trim() ?? "", ct);
        await SetAsync("Site.NameAccent", req.SiteNameAccent?.Trim() ?? "", ct);
        await SetAsync("Site.NameSize", req.SiteNameSize?.Trim() ?? "", ct);
        await SetAsync("Site.LogoUrl", req.LogoUrl?.Trim() ?? "", ct);
        await SetAsync("Site.FooterLogoUrl", req.FooterLogoUrl?.Trim() ?? "", ct);
        await SetAsync("Store.AddressLine", req.ContactAddress?.Trim() ?? "", ct);
        await SetAsync("Store.Mobile1", req.ContactMobile1?.Trim() ?? "", ct);
        await SetAsync("Store.Mobile2", req.ContactMobile2?.Trim() ?? "", ct);
        await SetAsync("Store.Landline1", req.ContactLandline1?.Trim() ?? "", ct);
        await SetAsync("Store.Landline2", req.ContactLandline2?.Trim() ?? "", ct);
        await SetAsync("Store.Email", req.ContactEmail?.Trim() ?? "", ct);
        await SetAsync("Store.Hours", req.ContactHours?.Trim() ?? "", ct);
        await SetAsync("Store.City", req.ContactCity?.Trim() ?? "", ct);

        // Per-state overrides are replaced wholesale — the admin screen always sends the
        // complete list, so a row removed there must disappear here.
        if (req.StateMinOrders is not null)
        {
            var existing = await _db.StateMinOrderAmounts.Where(x => x.TenantId == Tenant).ToListAsync(ct);
            _db.StateMinOrderAmounts.RemoveRange(existing);

            foreach (var row in req.StateMinOrders.Where(r => !string.IsNullOrWhiteSpace(r.StateName)))
            {
                _db.StateMinOrderAmounts.Add(new StateMinOrderAmount
                {
                    TenantId = Tenant,
                    StateName = row.StateName.Trim(),
                    MinOrderAmount = Math.Max(0, row.MinOrderAmount),
                    IsActive = row.IsActive,
                    CreatedAt = DateTime.UtcNow,
                });
            }
        }

        await _db.SaveChangesAsync(ct);
        return await Get(ct);
    }

    /// <summary>
    /// Sends a real test message with the saved settings. Save first — this reads what is
    /// stored, not what is on screen, which is also what makes it a genuine end-to-end check.
    /// </summary>
    [HttpPost("test-email")]
    public async Task<IActionResult> SendTestEmail(
        [FromBody] SendTestEmailRequest req,
        [FromServices] Notifications.ConfiguredEmailSender sender,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.To) || !req.To.Contains('@'))
            throw new AppException("Enter a valid email address to send the test to.");

        await sender.SendTestAsync(req.To.Trim(), ct);
        return Ok(ApiResponse<object>.Ok(new { sent = true }, $"Test email sent to {req.To.Trim()}."));
    }

    private async Task<Dictionary<string, string?>> LoadAsync(CancellationToken ct)
        => await _db.Settings
            .Where(x => x.SettingKey.StartsWith("QuickOrder.")
                     || x.SettingKey.StartsWith("Payment.")
                     || x.SettingKey.StartsWith("Email.")
                     || x.SettingKey.StartsWith("Site.")
                     || x.SettingKey.StartsWith("Store.")
                     || x.SettingKey == "Channels.EmailMode")
            .ToDictionaryAsync(x => x.SettingKey, x => x.SettingValue, ct);

    /// <summary>Upsert — a key seeded by migration exists; one added later may not.</summary>
    private async Task SetAsync(string key, string value, CancellationToken ct)
    {
        var row = await _db.Settings.FirstOrDefaultAsync(x => x.SettingKey == key, ct);
        if (row is null)
            _db.Settings.Add(new Setting { SettingKey = key, SettingValue = value, CreatedAt = DateTime.UtcNow });
        else
            row.SettingValue = value;
    }

    private static decimal Dec(Dictionary<string, string?> s, string key)
        => s.TryGetValue(key, out var raw) && decimal.TryParse(raw, out var v) ? v : 0m;

    private static string Str(Dictionary<string, string?> s, string key)
        => s.TryGetValue(key, out var raw) ? raw ?? "" : "";
}
