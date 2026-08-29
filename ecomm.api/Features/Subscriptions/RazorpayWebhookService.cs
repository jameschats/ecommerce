using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ecomm.api.Features.Subscriptions;

public sealed record RazorpayWebhookOutcome(bool Handled, string Message);

public interface IRazorpayWebhookService
{
    /// <summary>True when the raw body HMACs to the given X-Razorpay-Signature under Billing:WebhookSecret.</summary>
    bool VerifySignature(string rawBody, string? signature);
    Task<RazorpayWebhookOutcome> HandleAsync(string rawBody, CancellationToken ct);
}

/// <summary>
/// Razorpay-native billing webhook (platform collecting from merchants). Subscribe the endpoint to
/// <c>order.paid</c> in the Razorpay dashboard: that event carries BOTH the order (with our receipt,
/// <c>sub-{tenantId}-{planId}</c>) and the payment, so we can map the money back to a store without
/// a second API call. Recording is idempotent on the payment id, so this and the browser's
/// checkout/confirm can both fire safely — whichever lands first wins.
/// </summary>
public sealed class RazorpayWebhookService(ISubscriptionService subscriptions, IConfiguration config, ILogger<RazorpayWebhookService> logger)
    : IRazorpayWebhookService
{
    public bool VerifySignature(string rawBody, string? signature)
    {
        var secret = config["Billing:WebhookSecret"];
        if (string.IsNullOrEmpty(secret)) return true;    // dev: unconfigured = open (mirrors the shared-secret webhook)
        if (string.IsNullOrWhiteSpace(signature)) return false;

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var expected = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody))).ToLowerInvariant();
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(signature.Trim().ToLowerInvariant()));
    }

    public async Task<RazorpayWebhookOutcome> HandleAsync(string rawBody, CancellationToken ct)
    {
        JsonDocument doc;
        try { doc = JsonDocument.Parse(rawBody); }
        catch (JsonException) { return new RazorpayWebhookOutcome(false, "Malformed JSON."); }

        using (doc)
        {
            var root = doc.RootElement;
            var evt = root.TryGetProperty("event", out var e) ? e.GetString() : null;

            // Recurring auto-debit (Razorpay Subscriptions) — P2.
            if (evt is not null && evt.StartsWith("subscription.", StringComparison.Ordinal))
                return await HandleSubscriptionEventAsync(evt, root, ct);

            if (evt != "order.paid") return new RazorpayWebhookOutcome(false, $"Ignored event '{evt}'.");

            if (!root.TryGetProperty("payload", out var payload)
                || !payload.TryGetProperty("order", out var orderWrap) || !orderWrap.TryGetProperty("entity", out var order)
                || !payload.TryGetProperty("payment", out var payWrap) || !payWrap.TryGetProperty("entity", out var payment))
                return new RazorpayWebhookOutcome(false, "Missing order/payment entity.");

            var receipt = order.TryGetProperty("receipt", out var r) ? r.GetString() : null;
            var paymentId = payment.TryGetProperty("id", out var pid) ? pid.GetString() : null;
            if (string.IsNullOrWhiteSpace(paymentId)) return new RazorpayWebhookOutcome(false, "Missing payment id.");

            if (!TryParseReceipt(receipt, out var tenantId, out _))
            {
                logger.LogWarning("Razorpay order.paid with unrecognised receipt '{Receipt}' — ignoring.", receipt);
                return new RazorpayWebhookOutcome(false, "Receipt is not a subscription charge.");
            }

            // Amount is authoritative from Razorpay, in paise.
            var paise = order.TryGetProperty("amount", out var a) && a.TryGetInt64(out var v) ? v : 0L;
            var amount = paise / 100m;

            var now = DateTime.UtcNow;
            var recorded = await subscriptions.RecordChargeAsync(
                new RecordChargeCommand(tenantId, amount, paymentId!, null, now, now.AddMonths(1)), ct);

            logger.LogInformation("Razorpay order.paid tenant {TenantId} payment {PaymentId}: {Outcome}",
                tenantId, paymentId, recorded ? "recorded" : "duplicate-ignored");
            return new RazorpayWebhookOutcome(recorded, recorded ? "Charge recorded." : "Already processed.");
        }
    }

    /// <summary>
    /// subscription.* events. The tenant is read from the subscription's <c>notes.tenantId</c> (set when we
    /// created it); a charge carries the payment entity (amount in paise). Delegates to the idempotent
    /// subscription-event handler.
    /// </summary>
    private async Task<RazorpayWebhookOutcome> HandleSubscriptionEventAsync(string evt, JsonElement root, CancellationToken ct)
    {
        if (!root.TryGetProperty("payload", out var payload)
            || !payload.TryGetProperty("subscription", out var subWrap) || !subWrap.TryGetProperty("entity", out var sub))
            return new RazorpayWebhookOutcome(false, "Missing subscription entity.");

        var subscriptionId = sub.TryGetProperty("id", out var sid) ? sid.GetString() : null;
        long tenantId = 0;
        if (sub.TryGetProperty("notes", out var notes) && notes.ValueKind == JsonValueKind.Object
            && notes.TryGetProperty("tenantId", out var tid))
        {
            var raw = tid.ValueKind == JsonValueKind.String ? tid.GetString() : tid.GetRawText();
            long.TryParse(raw, out tenantId);
        }
        if (tenantId <= 0)
        {
            logger.LogWarning("Razorpay {Event} with no tenantId note (sub {Sub}) — ignoring.", evt, subscriptionId);
            return new RazorpayWebhookOutcome(false, "No tenant mapping on subscription.");
        }

        string? paymentId = null;
        decimal amount = 0m;
        if (payload.TryGetProperty("payment", out var payWrap) && payWrap.TryGetProperty("entity", out var payment))
        {
            paymentId = payment.TryGetProperty("id", out var pid) ? pid.GetString() : null;
            var paise = payment.TryGetProperty("amount", out var a) && a.TryGetInt64(out var v) ? v : 0L;
            amount = paise / 100m;
        }

        await subscriptions.HandleSubscriptionEventAsync(evt, tenantId, paymentId, amount, subscriptionId, ct);
        logger.LogInformation("Razorpay {Event} tenant {TenantId} sub {Sub} handled.", evt, tenantId, subscriptionId);
        return new RazorpayWebhookOutcome(true, "Subscription event handled.");
    }

    /// <summary>Receipts are minted as <c>sub-{tenantId}-{planId}</c> in SubscriptionService.StartCheckoutAsync.</summary>
    internal static bool TryParseReceipt(string? receipt, out long tenantId, out int planId)
    {
        tenantId = 0; planId = 0;
        if (string.IsNullOrWhiteSpace(receipt)) return false;
        var parts = receipt.Split('-');
        return parts.Length >= 3 && parts[0] == "sub"
               && long.TryParse(parts[1], out tenantId) && int.TryParse(parts[2], out planId)
               && tenantId > 0;
    }
}
