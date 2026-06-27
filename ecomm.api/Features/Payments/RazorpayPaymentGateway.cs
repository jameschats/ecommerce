using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ecomm.api.Common.Exceptions;

namespace ecomm.api.Features.Payments;

/// <summary>
/// Real Razorpay gateway (test or live keys). Amounts are sent in paise.
/// Activated when Payments:Provider=Razorpay and both keys are configured.
/// </summary>
public sealed class RazorpayPaymentGateway : IPaymentGateway
{
    private readonly HttpClient _http;
    private readonly string _keyId;
    private readonly string _keySecret;

    public RazorpayPaymentGateway(HttpClient http, string keyId, string keySecret)
    {
        _http = http;
        _keyId = keyId;
        _keySecret = keySecret;
        _http.BaseAddress ??= new Uri("https://api.razorpay.com/");
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{keyId}:{keySecret}"));
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basic);
    }

    public string Name => "Razorpay";
    public string? PublicKey => _keyId;

    public async Task<GatewayOrder> CreateOrderAsync(long orderId, decimal amount, string currency, string receipt, CancellationToken ct = default)
    {
        var paise = (long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero);
        var body = JsonSerializer.Serialize(new { amount = paise, currency, receipt, payment_capture = 1 });
        using var resp = await _http.PostAsync("v1/orders", new StringContent(body, Encoding.UTF8, "application/json"), ct);
        var json = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new AppException($"Razorpay order creation failed: {json}", 502);
        using var doc = JsonDocument.Parse(json);
        var id = doc.RootElement.GetProperty("id").GetString()!;
        return new GatewayOrder(id, amount, currency);
    }

    public bool VerifySignature(string gatewayOrderId, string gatewayPaymentId, string signature)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_keySecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes($"{gatewayOrderId}|{gatewayPaymentId}"));
        var expected = Convert.ToHexString(hash).ToLowerInvariant();
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(signature ?? string.Empty));
    }

    public async Task<GatewayRefund> RefundAsync(string gatewayPaymentId, decimal amount, CancellationToken ct = default)
    {
        var paise = (long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero);
        var body = JsonSerializer.Serialize(new { amount = paise });
        using var resp = await _http.PostAsync($"v1/payments/{gatewayPaymentId}/refund",
            new StringContent(body, Encoding.UTF8, "application/json"), ct);
        var json = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new AppException($"Razorpay refund failed: {json}", 502);
        using var doc = JsonDocument.Parse(json);
        var id = doc.RootElement.GetProperty("id").GetString()!;
        var status = doc.RootElement.TryGetProperty("status", out var s) ? s.GetString() ?? "processed" : "processed";
        return new GatewayRefund(id, status);
    }
}
