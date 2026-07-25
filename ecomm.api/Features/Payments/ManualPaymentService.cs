using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using Microsoft.EntityFrameworkCore;
using QRCoder;

namespace ecomm.api.Features.Payments;

public sealed record ManualPaymentDetailsDto(
    long OrderId, string OrderNumber, decimal Amount, string Status,
    string? UpiId, string? UpiPayeeName, string? UpiIntent, string? QrCodeDataUri,
    string? BankAccountName, string? BankAccountNumber, string? BankIfsc, string? BankName,
    string? ReportedReference, DateTime? ReportedAt, bool IsConfirmed);

public sealed record ReportPaymentRequest(string ReferenceNumber, string? ProofImageUrl);

public interface IManualPaymentService
{
    Task<ManualPaymentDetailsDto> GetDetailsAsync(long orderId, long userId, bool isAdmin, CancellationToken ct = default);
    Task<ManualPaymentDetailsDto> ReportAsync(long orderId, long userId, ReportPaymentRequest req, CancellationToken ct = default);
}

/// <summary>
/// Manual UPI / bank-transfer payment (design.md §8).
///
/// There is no gateway. The buyer is shown a QR and account details, pays with their own
/// app, and reports the reference; an admin confirms the money arrived. The Mock and
/// Razorpay gateways stay in the codebase unused.
/// </summary>
public sealed class ManualPaymentService : IManualPaymentService
{
    private const long Tenant = 1;

    private readonly EcommerceDbContext _db;

    public ManualPaymentService(EcommerceDbContext db) => _db = db;

    public async Task<ManualPaymentDetailsDto> GetDetailsAsync(
        long orderId, long userId, bool isAdmin, CancellationToken ct = default)
    {
        var order = await _db.Orders
            .FirstOrDefaultAsync(o => o.OrderId == orderId && o.TenantId == Tenant, ct)
            ?? throw new AppException("Order not found.", StatusCodes.Status404NotFound);

        // An order's payment details include the amount owed and the buyer's own reference,
        // so they are readable only by the buyer or an admin.
        if (!isAdmin && order.UserId != userId)
            throw new AppException("Order not found.", StatusCodes.Status404NotFound);

        var settings = await LoadPaymentSettingsAsync(ct);
        var upiId = Text(settings, "Payment.UpiId");
        var payee = Text(settings, "Payment.UpiPayeeName");

        string? intent = null;
        string? qr = null;
        if (!string.IsNullOrWhiteSpace(upiId))
        {
            intent = BuildUpiIntent(upiId!, payee, order.TotalAmount, order.OrderNumber);
            qr = BuildQrDataUri(intent);
        }

        var payment = await _db.Payments
            .Where(p => p.OrderId == orderId)
            .OrderByDescending(p => p.PaymentId)
            .FirstOrDefaultAsync(ct);

        return new ManualPaymentDetailsDto(
            order.OrderId, order.OrderNumber, order.TotalAmount, order.Status,
            upiId, payee, intent, qr,
            Text(settings, "Payment.BankAccountName"),
            Text(settings, "Payment.BankAccountNumber"),
            Text(settings, "Payment.BankIfsc"),
            Text(settings, "Payment.BankName"),
            payment?.ReferenceNumber, payment?.ReportedAt,
            string.Equals(order.Status, "Paid", StringComparison.OrdinalIgnoreCase));
    }

    public async Task<ManualPaymentDetailsDto> ReportAsync(
        long orderId, long userId, ReportPaymentRequest req, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.ReferenceNumber))
            throw new AppException("Enter the UPI or bank reference number.");

        var order = await _db.Orders
            .FirstOrDefaultAsync(o => o.OrderId == orderId && o.TenantId == Tenant && o.UserId == userId, ct)
            ?? throw new AppException("Order not found.", StatusCodes.Status404NotFound);

        if (string.Equals(order.Status, "Paid", StringComparison.OrdinalIgnoreCase))
            throw new AppException("This order is already marked as paid.");

        var now = DateTime.UtcNow;
        var payment = await _db.Payments
            .Where(p => p.OrderId == orderId)
            .OrderByDescending(p => p.PaymentId)
            .FirstOrDefaultAsync(ct);

        if (payment is null)
        {
            payment = new Data.Entities.Payment
            {
                OrderId = orderId,
                Method = "Manual",
                Amount = order.TotalAmount,
                Currency = "INR",
                Status = "Reported",
                CreatedAt = now,
            };
            _db.Payments.Add(payment);
        }

        payment.ReferenceNumber = req.ReferenceNumber.Trim();
        payment.ProofImageUrl = req.ProofImageUrl;
        payment.ReportedAt = now;
        payment.Status = "Reported";

        // Move the ORDER to PaymentReported too (design.md §8: PendingPayment →
        // PaymentReported → Paid). Recording this only on the payment row left the admin
        // order list unable to tell "waiting for the buyer to pay" from "buyer says they
        // paid, needs checking" — which is the one distinction that queue exists to make.
        //
        // This is still only a claim: PaymentReported is not a paid state, it grants
        // nothing, and only an admin confirmation moves it to Paid.
        order.Status = "PaymentReported";
        order.UpdatedAt = now;

        await _db.SaveChangesAsync(ct);
        return await GetDetailsAsync(orderId, userId, isAdmin: false, ct);
    }

    /// <summary>
    /// A UPI intent URI. Amount and order number are embedded so the buyer cannot mistype
    /// the amount, and the reference comes back on the transaction for reconciliation.
    /// </summary>
    private static string BuildUpiIntent(string upiId, string? payeeName, decimal amount, string orderNumber)
    {
        var pn = Uri.EscapeDataString(string.IsNullOrWhiteSpace(payeeName) ? "Merchant" : payeeName!);
        var tn = Uri.EscapeDataString($"Order {orderNumber}");
        return $"upi://pay?pa={Uri.EscapeDataString(upiId)}&pn={pn}&am={amount:0.00}&cu=INR&tn={tn}";
    }

    /// <summary>
    /// PNG data URI, so the page needs no image endpoint and no external request — which
    /// matters on a payment screen where a blocked or slow asset looks like a broken page.
    /// </summary>
    private static string BuildQrDataUri(string payload)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.M);
        var png = new PngByteQRCode(data).GetGraphic(10);
        return $"data:image/png;base64,{Convert.ToBase64String(png)}";
    }

    private async Task<Dictionary<string, string?>> LoadPaymentSettingsAsync(CancellationToken ct)
        => await _db.Settings
            .Where(s => s.SettingKey.StartsWith("Payment."))
            .ToDictionaryAsync(s => s.SettingKey, s => s.SettingValue, ct);

    private static string? Text(Dictionary<string, string?> settings, string key)
        => settings.TryGetValue(key, out var raw) && !string.IsNullOrWhiteSpace(raw) ? raw : null;
}
