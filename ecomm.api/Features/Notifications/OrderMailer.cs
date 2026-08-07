using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Notifications;

public interface IOrderMailer
{
    Task SendOrderPlacedAsync(long orderId, string? customerEmail, CancellationToken ct = default);
    Task SendPaymentConfirmedAsync(long orderId, CancellationToken ct = default);
    Task SendDispatchedAsync(long orderId, string? courier, string? trackingNumber, CancellationToken ct = default);
    Task SendDeliveredAsync(long orderId, CancellationToken ct = default);

    /// <summary>
    /// The same message text the emails carry, as plain text for WhatsApp click-to-send
    /// (design.md §9.4). Returns null when there is nothing sensible to send.
    /// </summary>
    Task<(string Mobile, string Message)?> BuildWhatsAppMessageAsync(
        long orderId, string kind, CancellationToken ct = default);
}

/// <summary>
/// Transactional emails for the quick-order flow (design.md §9.3).
///
/// Every method here swallows its own failures. A mail server being slow, misconfigured or
/// down must never fail an order that has already been written to the database — the buyer
/// would see an error for an order that actually exists, and might place it twice.
/// </summary>
public sealed class OrderMailer : IOrderMailer
{
    private readonly EcommerceDbContext _db;
    private readonly IEmailSender _email;
    private readonly INotificationFeedService _feed;
    private readonly ILogger<OrderMailer> _logger;

    private readonly INotificationPolicy _policy;

    public OrderMailer(
        EcommerceDbContext db,
        IEmailSender email,
        INotificationFeedService feed,
        INotificationPolicy policy,
        ILogger<OrderMailer> logger)
    {
        _db = db;
        _email = email;
        _feed = feed;
        _policy = policy;
        _logger = logger;
    }

    public async Task SendOrderPlacedAsync(long orderId, string? customerEmail, CancellationToken ct = default)
    {
        try
        {
            var order = await LoadAsync(orderId, ct);
            if (order is null) return;

            var items = await _db.OrderItems.Where(i => i.OrderId == orderId).ToListAsync(ct);
            var siteUrl = await SettingAsync("Site.Url", ct) ?? "https://daily.calendarshop.online";
            var payUrl = $"{siteUrl.TrimEnd('/')}/order/{orderId}/pay";

            var body = Wrap($@"
<h2 style=""margin:0 0 4px"">Thank you for your order</h2>
<p style=""margin:0 0 16px;color:#475569"">Order <strong>{order.OrderNumber}</strong></p>
{ItemsTable(items, order)}
<p style=""margin:20px 0 8px""><strong>Your order is not confirmed until payment is received.</strong></p>
<p style=""margin:0 0 20px"">
  <a href=""{payUrl}"" style=""background:#4f46e5;color:#fff;padding:12px 22px;border-radius:8px;
     text-decoration:none;display:inline-block;font-weight:600"">Pay now — view UPI QR &amp; bank details</a>
</p>
<p style=""margin:0;color:#64748b;font-size:13px"">
  Once you have paid, enter your UPI or bank reference on that page so we can match your payment.
</p>");

            if (!string.IsNullOrWhiteSpace(customerEmail)
                && await _policy.IsEnabledAsync("OrderPlaced", "Email", ct))
                await _email.SendAsync(customerEmail!, $"Order {order.OrderNumber} received — payment pending", body, ct);

            // Admin alert. Separate try/catch: the shop missing an alert must not stop the
            // customer's own confirmation from going out.
            var adminTo = await SettingAsync("Email.AdminNotifyTo", ct);
            if (!string.IsNullOrWhiteSpace(adminTo)
                && await _policy.IsEnabledAsync("OrderPlacedAdmin", "Email", ct))
            {
                var adminBody = Wrap($@"
<h2 style=""margin:0 0 4px"">New order {order.OrderNumber}</h2>
<p style=""margin:0 0 16px;color:#475569"">₹{order.TotalAmount:N0} · {items.Count} line(s)</p>
{ItemsTable(items, order)}
<h3 style=""margin:20px 0 6px;font-size:15px"">Delivery details</h3>
<pre style=""margin:0;font:13px/1.6 ui-monospace,monospace;white-space:pre-wrap;color:#334155"">{System.Net.WebUtility.HtmlEncode(order.Notes)}</pre>");

                // ₹ written literally, not via :C — the server runs invariant culture, so
                // :C0 renders the generic currency sign ¤ rather than a rupee symbol.
                await _email.SendAsync(adminTo!, $"New order {order.OrderNumber} — ₹{order.TotalAmount:N0}", adminBody, ct);
            }

            // In-app bell + admin notifications page. The base platform's own checkout does
            // this; the quick-order path bypasses that service entirely, so it had to be
            // wired here or the notifications page would stay permanently empty.
            await _feed.NotifyAdminsAsync(
                "NewOrder",
                "New order received",
                $"Order {order.OrderNumber} · ₹{order.TotalAmount:N0}",
                "/admin/payments",
                ct);

            await _feed.NotifyUserAsync(
                order.UserId, "OrderUpdate", "Order placed",
                $"Order {order.OrderNumber} — payment pending", $"/order/{orderId}/pay", ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send order-placed email for order {OrderId}", orderId);
        }
    }

    public async Task SendPaymentConfirmedAsync(long orderId, CancellationToken ct = default)
    {
        try
        {
            var order = await LoadAsync(orderId, ct);
            if (order is null) return;

            var to = await RecipientAsync(order, ct);
            if (string.IsNullOrWhiteSpace(to)) return;
            if (!await _policy.IsEnabledAsync("PaymentConfirmed", "Email", ct)) return;

            var items = await _db.OrderItems.Where(i => i.OrderId == orderId).ToListAsync(ct);

            await _email.SendAsync(to!, $"Payment received for order {order.OrderNumber}", Wrap($@"
<h2 style=""margin:0 0 4px"">Payment received</h2>
<p style=""margin:0 0 16px;color:#475569"">
  We have confirmed your payment for order <strong>{order.OrderNumber}</strong>. We are preparing it for dispatch.
</p>
{ItemsTable(items, order)}
<p style=""margin:20px 0 0;color:#64748b;font-size:13px"">
  We will contact you with courier and tracking details once your order is dispatched.
</p>"), ct);

            await _feed.NotifyUserAsync(
                order.UserId, "OrderUpdate", "Payment received",
                $"Order {order.OrderNumber} confirmed", $"/account/orders", ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send payment-confirmed email for order {OrderId}", orderId);
        }
    }

    public async Task SendDispatchedAsync(
        long orderId, string? courier, string? trackingNumber, CancellationToken ct = default)
    {
        try
        {
            var order = await LoadAsync(orderId, ct);
            if (order is null) return;

            var to = await RecipientAsync(order, ct);
            if (string.IsNullOrWhiteSpace(to)) return;

            var tracking = string.IsNullOrWhiteSpace(trackingNumber)
                ? ""
                : $@"<p style=""margin:0 0 6px"">Tracking number:
                       <strong style=""font-family:ui-monospace,monospace"">{System.Net.WebUtility.HtmlEncode(trackingNumber)}</strong></p>";

            await _email.SendAsync(to!, $"Order {order.OrderNumber} has been dispatched", Wrap($@"
<h2 style=""margin:0 0 4px"">Your order is on its way</h2>
<p style=""margin:0 0 16px;color:#475569"">Order <strong>{order.OrderNumber}</strong></p>
<p style=""margin:0 0 6px"">Courier: <strong>{System.Net.WebUtility.HtmlEncode(courier ?? "—")}</strong></p>
{tracking}
<p style=""margin:16px 0 0;color:#64748b;font-size:13px"">
  Parcels travel by transport service to your city. You will be contacted when it is ready to collect.
</p>"), ct);

            await _feed.NotifyUserAsync(
                order.UserId, "OrderUpdate", "Order dispatched",
                $"Order {order.OrderNumber} is on its way", "/account/orders", ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send dispatched email for order {OrderId}", orderId);
        }
    }

    public async Task SendDeliveredAsync(long orderId, CancellationToken ct = default)
    {
        try
        {
            var order = await LoadAsync(orderId, ct);
            if (order is null) return;

            var to = await RecipientAsync(order, ct);
            if (string.IsNullOrWhiteSpace(to)) return;

            await _email.SendAsync(to!, $"Order {order.OrderNumber} delivered", Wrap($@"
<h2 style=""margin:0 0 4px"">Delivered</h2>
<p style=""margin:0 0 16px;color:#475569"">
  Order <strong>{order.OrderNumber}</strong> has been marked delivered. Thank you for your business.
</p>
<p style=""margin:0;color:#64748b;font-size:13px"">
  If anything is missing or damaged, reply to this email and we will sort it out.
</p>"), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send delivered email for order {OrderId}", orderId);
        }
    }

    public async Task<(string Mobile, string Message)?> BuildWhatsAppMessageAsync(
        long orderId, string kind, CancellationToken ct = default)
    {
        var order = await LoadAsync(orderId, ct);
        if (order is null) return null;

        var mobile = FieldFromNotes(order.Notes, "Mobile");
        if (string.IsNullOrWhiteSpace(mobile)) return null;

        var name = FieldFromNotes(order.Notes, "Name") ?? "there";
        var siteUrl = await SettingAsync("Site.Url", ct) ?? "https://daily.calendarshop.online";

        var shipment = await _db.Shipments
            .Where(s => s.OrderId == orderId)
            .OrderByDescending(s => s.ShipmentId)
            .FirstOrDefaultAsync(ct);

        var message = kind switch
        {
            "placed" =>
                $"Hi {name}, we have received your order {order.OrderNumber} for ₹{order.TotalAmount:N0}. "
                + $"Please complete payment here: {siteUrl.TrimEnd('/')}/order/{orderId}/pay",
            "paid" =>
                $"Hi {name}, we have received your payment for order {order.OrderNumber} (₹{order.TotalAmount:N0}). "
                + "We are preparing it for dispatch.",
            "dispatched" =>
                $"Hi {name}, your order {order.OrderNumber} has been dispatched"
                + (string.IsNullOrWhiteSpace(shipment?.Courier) ? "" : $" via {shipment!.Courier}")
                + (string.IsNullOrWhiteSpace(shipment?.TrackingNumber) ? "" : $". Tracking: {shipment!.TrackingNumber}")
                + ".",
            "delivered" =>
                $"Hi {name}, your order {order.OrderNumber} has been marked delivered. Thank you for your business.",
            _ => $"Hi {name}, an update on your order {order.OrderNumber}.",
        };

        // India-only in Phase 1, so a bare 10-digit number gets the country code.
        var digits = new string(mobile.Where(char.IsDigit).ToArray());
        if (digits.Length == 10) digits = "91" + digits;

        return (digits, message);
    }

    // ------------------------------------------------------------------ helpers

    private async Task<string?> RecipientAsync(Order order, CancellationToken ct)
        => FieldFromNotes(order.Notes, "Email")
           ?? await _db.Users.Where(u => u.UserId == order.UserId).Select(u => u.Email).FirstOrDefaultAsync(ct);

    private Task<Order?> LoadAsync(long orderId, CancellationToken ct)
        => _db.Orders.FirstOrDefaultAsync(o => o.OrderId == orderId, ct);

    private async Task<string?> SettingAsync(string key, CancellationToken ct)
    {
        var v = await _db.Settings.Where(s => s.SettingKey == key).Select(s => s.SettingValue).FirstOrDefaultAsync(ct);
        return string.IsNullOrWhiteSpace(v) ? null : v;
    }

    /// <summary>
    /// The quick-order form captures the email as free text in Notes rather than against
    /// the account, because a dealer may order for someone else. Prefer it over the
    /// account address, which for a mobile-OTP login may not exist at all.
    /// </summary>
    /// <summary>Delegates to the shared reader so OrderService resolves recipients identically.</summary>
    private static string? FieldFromNotes(string? notes, string field) =>
        Orders.OrderNotes.Field(notes, field);

    private static string ItemsTable(List<OrderItem> items, Order order)
    {
        var rows = string.Join("", items.Select(i => $@"
<tr>
  <td style=""padding:8px 0;border-bottom:1px solid #e2e8f0"">{System.Net.WebUtility.HtmlEncode(i.ProductName)}
    <span style=""color:#94a3b8"">× {i.Quantity}</span></td>
  <td style=""padding:8px 0;border-bottom:1px solid #e2e8f0;text-align:right"">₹{i.LineTotal:N2}</td>
</tr>"));

        return $@"
<table style=""width:100%;border-collapse:collapse;font-size:14px"">
  {rows}
  <tr><td style=""padding:10px 0;font-weight:700"">Total</td>
      <td style=""padding:10px 0;text-align:right;font-weight:700;font-size:17px"">₹{order.TotalAmount:N2}</td></tr>
</table>";
    }

    private static string Wrap(string inner) => $@"
<div style=""font-family:system-ui,-apple-system,'Segoe UI',sans-serif;max-width:560px;margin:0 auto;
            padding:24px;color:#0f172a;line-height:1.55"">
  {inner}
  <hr style=""margin:28px 0 12px;border:none;border-top:1px solid #e2e8f0"" />
  <p style=""margin:0;color:#94a3b8;font-size:12px"">DailyCalendarShop</p>
</div>";
}
