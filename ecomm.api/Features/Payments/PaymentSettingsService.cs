using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Payments;

public sealed record PaymentSettingsDto(
    string Provider, string? RazorpayKeyId, bool HasSecret, bool IsEnabled, bool CodEnabled);

public sealed record UpdatePaymentSettingsRequest(
    string Provider, string? RazorpayKeyId, string? RazorpayKeySecret, bool IsEnabled, bool CodEnabled);

public interface IPaymentSettingsService
{
    Task<PaymentSettingsDto> GetAsync(CancellationToken ct = default);
    Task<PaymentSettingsDto> UpdateAsync(UpdatePaymentSettingsRequest req, CancellationToken ct = default);
}

/// <summary>
/// Per-tenant payment configuration surfaced to the merchant admin. The Razorpay secret is
/// encrypted with the shared <c>ProtectorPurpose</c> so the tenant-aware IPaymentGateway
/// factory can decrypt it. The secret is write-only — <see cref="PaymentSettingsDto"/> exposes
/// only <c>HasSecret</c>. COD stays in the generic Settings table (shared with store settings).
/// </summary>
public sealed class PaymentSettingsService(EcommerceDbContext db, IDataProtectionProvider dp) : IPaymentSettingsService
{
    /// <summary>Data-protection purpose shared with the gateway factory in Program.cs.</summary>
    public const string ProtectorPurpose = "payments.razorpay.secret.v1";
    private static readonly string[] Providers = { "Mock", "Razorpay" };

    private long Tenant => db.CurrentTenantId;
    private IDataProtector Protector => dp.CreateProtector(ProtectorPurpose);

    public async Task<PaymentSettingsDto> GetAsync(CancellationToken ct = default)
    {
        var acct = await db.TenantPaymentAccounts.FirstOrDefaultAsync(ct);
        var cod = await CodEnabledAsync(ct);
        return new PaymentSettingsDto(
            acct?.Provider ?? "Mock", acct?.RazorpayKeyId,
            !string.IsNullOrEmpty(acct?.RazorpayKeySecret), acct?.IsEnabled ?? false, cod);
    }

    public async Task<PaymentSettingsDto> UpdateAsync(UpdatePaymentSettingsRequest req, CancellationToken ct = default)
    {
        var provider = Providers.FirstOrDefault(p => p.Equals(req.Provider, StringComparison.OrdinalIgnoreCase))
            ?? throw new AppException("Provider must be Mock or Razorpay.");

        var acct = await db.TenantPaymentAccounts.FirstOrDefaultAsync(ct);
        if (acct is null)
        {
            acct = new TenantPaymentAccount { Provider = provider, CreatedAt = DateTime.UtcNow };
            db.TenantPaymentAccounts.Add(acct);
        }
        acct.Provider = provider;
        acct.RazorpayKeyId = req.RazorpayKeyId?.Trim();
        // Only re-encrypt when a new secret is supplied (blank = keep existing).
        if (!string.IsNullOrWhiteSpace(req.RazorpayKeySecret))
            acct.RazorpayKeySecret = Protector.Protect(req.RazorpayKeySecret.Trim());

        var razorpayReady = provider == "Razorpay"
            && !string.IsNullOrWhiteSpace(acct.RazorpayKeyId) && !string.IsNullOrEmpty(acct.RazorpayKeySecret);
        if (req.IsEnabled && provider == "Razorpay" && !razorpayReady)
            throw new AppException("Add your Razorpay Key ID and Secret before enabling live payments.");
        acct.IsEnabled = req.IsEnabled;
        acct.UpdatedAt = DateTime.UtcNow;
        if (acct.IsEnabled && acct.ConnectedAt is null) acct.ConnectedAt = DateTime.UtcNow;

        await SetCodAsync(req.CodEnabled, ct);
        await db.SaveChangesAsync(ct);
        return await GetAsync(ct);
    }

    // COD lives in the shared Settings table (same key store settings uses).
    private async Task<bool> CodEnabledAsync(CancellationToken ct) =>
        string.Equals(await db.Settings.Where(s => s.TenantId == Tenant && s.SettingKey == "CodEnabled")
            .Select(s => s.SettingValue).FirstOrDefaultAsync(ct), "true", StringComparison.OrdinalIgnoreCase);

    private async Task SetCodAsync(bool enabled, CancellationToken ct)
    {
        var existing = await db.Settings.FirstOrDefaultAsync(s => s.TenantId == Tenant && s.SettingKey == "CodEnabled", ct);
        if (existing is null)
            db.Settings.Add(new Setting { TenantId = Tenant, SettingKey = "CodEnabled", SettingValue = enabled ? "true" : "false", DataType = "string", Category = "Billing", CreatedAt = DateTime.UtcNow });
        else
            existing.SettingValue = enabled ? "true" : "false";
    }
}
