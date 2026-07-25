using System.Security.Claims;
using ecomm.api.Common.Exceptions;
using ecomm.api.Common.Models;
using ecomm.api.Data.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Payments;

public sealed record PendingPaymentDto(
    long OrderId, string OrderNumber, decimal Amount, string Status,
    string? ReferenceNumber, DateTime? ReportedAt, DateTime PlacedAt, string? CustomerNotes);

/// <summary>Manual payment — buyer-facing details and reporting (design.md §8).</summary>
[ApiController]
[Route("api/payments/manual")]
[Authorize]
public sealed class ManualPaymentController : ControllerBase
{
    private readonly IManualPaymentService _payments;

    public ManualPaymentController(IManualPaymentService payments) => _payments = payments;

    private long UserId => long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
        ? id
        : throw new AppException("Please sign in.", StatusCodes.Status401Unauthorized);

    private bool IsAdmin => User.IsInRole("Admin");

    /// <summary>Payment instructions for an order: QR, UPI intent and bank details.</summary>
    [HttpGet("{orderId:long}")]
    public async Task<IActionResult> Details(long orderId, CancellationToken ct)
        => Ok(ApiResponse<ManualPaymentDetailsDto>.Ok(await _payments.GetDetailsAsync(orderId, UserId, IsAdmin, ct)));

    /// <summary>Buyer reports they have paid. Records the claim; does not mark the order paid.</summary>
    [HttpPost("{orderId:long}/report")]
    public async Task<IActionResult> Report(long orderId, [FromBody] ReportPaymentRequest req, CancellationToken ct)
        => Ok(ApiResponse<ManualPaymentDetailsDto>.Ok(
            await _payments.ReportAsync(orderId, UserId, req, ct),
            "Thanks — we will confirm your payment shortly."));
}

/// <summary>The admin side of manual payment: the queue and the confirmation.</summary>
[ApiController]
[Route("api/admin/payments")]
[Authorize(Roles = "Admin")]
public sealed class AdminPaymentsController : ControllerBase
{
    private readonly EcommerceDbContext _db;

    public AdminPaymentsController(EcommerceDbContext db) => _db = db;

    /// <summary>
    /// Orders awaiting payment confirmation. Reported ones first — those are the ones with
    /// a buyer waiting on a human to check a bank statement.
    /// </summary>
    [HttpGet("pending")]
    public async Task<IActionResult> Pending(CancellationToken ct)
    {
        var rows = await _db.Orders
            .Where(o => o.TenantId == 1 && (o.Status == "Pending" || o.Status == "PaymentReported"))
            .GroupJoin(_db.Payments, o => o.OrderId, p => p.OrderId, (o, ps) => new { o, ps })
            .SelectMany(x => x.ps.OrderByDescending(p => p.PaymentId).Take(1).DefaultIfEmpty(),
                (x, p) => new PendingPaymentDto(
                    x.o.OrderId, x.o.OrderNumber, x.o.TotalAmount, x.o.Status,
                    p != null ? p.ReferenceNumber : null,
                    p != null ? p.ReportedAt : null,
                    x.o.PlacedAt ?? x.o.CreatedAt,
                    x.o.Notes))
            .ToListAsync(ct);

        var ordered = rows
            .OrderByDescending(r => r.ReportedAt.HasValue)
            .ThenBy(r => r.ReportedAt ?? r.PlacedAt)
            .ToList();

        return Ok(ApiResponse<List<PendingPaymentDto>>.Ok(ordered));
    }

    /// <summary>
    /// Confirms the money arrived: the order becomes Paid and the reserved stock is
    /// committed (reserved → gone), which is the point of no return for that inventory.
    /// </summary>
    [HttpPost("{orderId:long}/confirm")]
    public async Task<IActionResult> Confirm(
        long orderId,
        [FromServices] Notifications.IOrderMailer mailer,
        CancellationToken ct)
    {
        var adminId = long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : (long?)null;

        var order = await _db.Orders.FirstOrDefaultAsync(o => o.OrderId == orderId && o.TenantId == 1, ct)
            ?? throw new AppException("Order not found.", StatusCodes.Status404NotFound);

        if (string.Equals(order.Status, "Paid", StringComparison.OrdinalIgnoreCase))
            throw new AppException("This order is already marked as paid.");

        var now = DateTime.UtcNow;
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        order.Status = "Paid";
        order.UpdatedAt = now;

        var payment = await _db.Payments
            .Where(p => p.OrderId == orderId)
            .OrderByDescending(p => p.PaymentId)
            .FirstOrDefaultAsync(ct);

        if (payment is null)
        {
            payment = new Data.Entities.Payment
            {
                OrderId = orderId, Method = "Manual", Amount = order.TotalAmount,
                Currency = "INR", CreatedAt = now,
            };
            _db.Payments.Add(payment);
        }
        payment.Status = "Success";
        payment.ConfirmedAt = now;
        payment.ConfirmedBy = adminId;

        // Commit the reservation: the goods are now sold, not merely held.
        var items = await _db.OrderItems.Where(i => i.OrderId == orderId).ToListAsync(ct);
        foreach (var item in items)
        {
            var inventory = await _db.Inventory
                .Where(i => i.TenantId == 1 && i.ProductId == item.ProductId && i.ReservedQty > 0)
                .OrderByDescending(i => i.ReservedQty)
                .FirstOrDefaultAsync(ct);

            if (inventory is not null)
                inventory.ReservedQty -= Math.Min(inventory.ReservedQty, item.Quantity);
        }

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        await mailer.SendPaymentConfirmedAsync(orderId, ct);

        return Ok(ApiResponse<object>.Ok(new { orderId, status = order.Status }, "Payment confirmed."));
    }
}
