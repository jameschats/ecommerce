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
    // Per-state minimum order overrides
    IReadOnlyList<StateMinOrderRow> StateMinOrders);

public sealed record StateMinOrderRow(long? Id, string StateName, decimal MinOrderAmount, bool IsActive);

public sealed record SaveShopSettingsRequest(
    decimal MinOrderAmount, decimal PackingChargePct, bool RoundOffEnabled,
    string? AnnouncementText, string? PriceValidUpto,
    string? UpiId, string? UpiPayeeName,
    string? BankAccountName, string? BankAccountNumber, string? BankIfsc, string? BankName,
    IReadOnlyList<StateMinOrderRow>? StateMinOrders);

/// <summary>
/// Shop and payment configuration for the quick-order flow (design.md §10.3).
///
/// These live in the database rather than appsettings.json for one reason: the person who
/// needs to change a UPI ID or a packing charge cannot edit a JSON file on a server.
/// </summary>
[ApiController]
[Route("api/admin/shop-settings")]
[Authorize(Roles = "Admin")]
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

    private async Task<Dictionary<string, string?>> LoadAsync(CancellationToken ct)
        => await _db.Settings
            .Where(x => x.SettingKey.StartsWith("QuickOrder.") || x.SettingKey.StartsWith("Payment."))
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
