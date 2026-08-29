using ecomm.api.Data.Context;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ecomm.api.Features.Payments;

/// <summary>
/// Builds the PLATFORM's payment gateway — used when a merchant pays the platform (subscriptions,
/// AI credit top-ups). Deliberately distinct from the tenant-aware <see cref="IPaymentGateway"/>
/// registration, which routes a shopper's payment to the MERCHANT's own account.
///
/// Config precedence: the <c>PlatformPaymentSettings</c> row (set in super-admin) wins; otherwise the
/// app-wide <c>Payments</c> env config; otherwise the deterministic <see cref="MockPaymentGateway"/>.
/// Scoped rather than singleton because it reads the database.
/// </summary>
public sealed class PlatformPaymentGatewayFactory(
    IHttpClientFactory http,
    IOptions<PaymentOptions> options,
    EcommerceDbContext db,
    IDataProtectionProvider dp)
{
    public IPaymentGateway Create()
    {
        var (provider, keyId, secret) = Resolve();
        if (provider.Equals("Razorpay", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(keyId) && !string.IsNullOrWhiteSpace(secret))
            return new RazorpayPaymentGateway(http.CreateClient("razorpay"), keyId!, secret!);
        return new MockPaymentGateway();
    }

    /// <summary>The recurring (auto-debit) gateway, resolved from the same platform provider/keys as <see cref="Create"/>.</summary>
    public IRecurringBillingGateway CreateRecurring()
    {
        var (provider, keyId, secret) = Resolve();
        if (provider.Equals("Razorpay", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(keyId) && !string.IsNullOrWhiteSpace(secret))
            return new RazorpaySubscriptionGateway(http.CreateClient("razorpay"), keyId!, secret!);
        return new MockRecurringGateway();
    }

    /// <summary>Which provider/key is actually in force, and whether it came from the console or env.</summary>
    public (string Provider, string? KeyId, bool HasSecret, string Source) Describe()
    {
        var row = db.PlatformPaymentSettings.AsNoTracking().FirstOrDefault();
        var (provider, keyId, secret) = Resolve();
        return (provider, keyId, !string.IsNullOrWhiteSpace(secret), row is null ? "env" : "console");
    }

    private (string Provider, string? KeyId, string? Secret) Resolve()
    {
        var row = db.PlatformPaymentSettings.AsNoTracking().FirstOrDefault();
        if (row is not null)
        {
            string? secret = null;
            if (!string.IsNullOrEmpty(row.RazorpayKeySecret))
            {
                // Corrupt/rotated key material must not take platform billing offline.
                try { secret = dp.CreateProtector(PaymentSettingsService.ProtectorPurpose).Unprotect(row.RazorpayKeySecret); }
                catch { secret = null; }
            }
            return (row.Provider, row.RazorpayKeyId, secret);
        }
        var o = options.Value;
        return (o.Provider, o.RazorpayKeyId, o.RazorpayKeySecret);
    }
}
