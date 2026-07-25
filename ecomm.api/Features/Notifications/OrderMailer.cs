using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Notifications;

public interface IOrderMailer
{
    Task SendOrderPlacedAsync(long orderId, string? customerEmail, CancellationToken ct = default);
    Task SendPaymentConfirmedAsync(long orderId, CancellationToken ct = default);
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
    private readonly ILogger<OrderMailer> _logger;

    public OrderMailer(EcommerceDbContext db, IEmailSender email, ILogger<OrderMailer> logger)
    {
        _db = db;
        _email = email;
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

            if (!string.IsNullOrWhiteSpace(customerEmail))
                await _email.SendAsync(customerEmail!, $"Order {order.OrderNumber} received — payment pending", body, ct);

            // Admin alert. Separate try/catch: the shop missing an alert must not stop the
            // customer's own confirmation from going out.
            var adminTo = await SettingAsync("Email.AdminNotifyTo", ct);
            if (!string.IsNullOrWhiteSpace(adminTo))
            {
                var adminBody = Wrap($@"
<h2 style=""margin:0 0 4px"">New order {order.OrderNumber}</h2>
<p style=""margin:0 0 16px;color:#475569"">{order.TotalAmount:C0} · {items.Count} line(s)</p>
{ItemsTable(items, order)}
<h3 style=""margin:20px 0 6px;font-size:15px"">Delivery details</h3>
<pre style=""margin:0;font:13px/1.6 ui-monospace,monospace;white-space:pre-wrap;color:#334155"">{System.Net.WebUtility.HtmlEncode(order.Notes)}</pre>");

                await _email.SendAsync(adminTo!, $"New order {order.OrderNumber} — {order.TotalAmount:C0}", adminBody, ct);
            }
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

            var to = EmailFromNotes(order.Notes)
                     ?? await _db.Users.Where(u => u.UserId == order.UserId).Select(u => u.Email).FirstOrDefaultAsync(ct);
            if (string.IsNullOrWhiteSpace(to)) return;

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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send payment-confirmed email for order {OrderId}", orderId);
        }
    }

    // ------------------------------------------------------------------ helpers

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
    private static string? EmailFromNotes(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes)) return null;
        foreach (var line in notes.Split('\n'))
        {
            if (!line.StartsWith("Email:", StringComparison.OrdinalIgnoreCase)) continue;
            var value = line["Email:".Length..].Trim();
            return value.Contains('@') ? value : null;
        }
        return null;
    }

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
