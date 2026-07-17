using Microsoft.Extensions.Options;

namespace ecomm.api.Features.Payments;

/// <summary>
/// Builds the PLATFORM's payment gateway from the app-wide <c>Payments</c> config. Used when a merchant
/// pays the platform (AI credit top-ups; later, subscriptions). This is deliberately distinct from the
/// tenant-aware <see cref="IPaymentGateway"/> registration, which routes a shopper's payment to the
/// MERCHANT's own account. Falls back to the deterministic <see cref="MockPaymentGateway"/> in dev.
/// </summary>
public sealed class PlatformPaymentGatewayFactory(IHttpClientFactory http, IOptions<PaymentOptions> options)
{
    public IPaymentGateway Create()
    {
        var o = options.Value;
        if (o.Provider.Equals("Razorpay", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(o.RazorpayKeyId) && !string.IsNullOrWhiteSpace(o.RazorpayKeySecret))
            return new RazorpayPaymentGateway(http.CreateClient("razorpay"), o.RazorpayKeyId!, o.RazorpayKeySecret!);
        return new MockPaymentGateway();
    }
}
