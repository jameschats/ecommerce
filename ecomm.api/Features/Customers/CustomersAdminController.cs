using ecomm.api.Common.Models;
using ecomm.api.Common.Security;
using ecomm.api.Data.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Customers;

public sealed record AdminCustomerDto(
    long UserId, string? FullName, string? Email, string? PhoneNumber,
    bool IsActive, int Orders, decimal TotalSpent, DateTime? LastOrderAt, DateTime CreatedAt);

/// <summary>
/// The people who buy, as opposed to the people who write in.
///
/// They are a different list from Contacts and always have been — a customer is an account
/// with orders behind it, an enquiry is a message. Until now only the staff screen listed
/// users at all, and that sits behind user.manage, which is administrator-only. Reading the
/// customer list is part of managing customers, so it belongs here under customer.view.
/// </summary>
[ApiController]
[Route("api/admin/customers")]
[Authorize(Policy = Perm.CustomerView)]
public sealed class CustomersAdminController : ControllerBase
{
    private const long Tenant = 1;
    private static readonly string[] SoldStatuses = { "Paid", "Confirmed", "Packed", "Shipped", "Delivered" };

    private readonly EcommerceDbContext _db;
    public CustomersAdminController(EcommerceDbContext db) => _db = db;

    /// <param name="withOrders">Only those who have actually bought something.</param>
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? search, [FromQuery] bool withOrders = false,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
    {
        var p = Math.Max(1, page);
        var size = Math.Clamp(pageSize, 1, 100);

        // Anyone who is not staff. Every self-registered account holds the Customer role, and
        // an account stripped of every role is a former staff member rather than a buyer, so
        // the test is "holds Customer" rather than "holds no admin role".
        var q = _db.Users.AsNoTracking()
            .Where(u => u.TenantId == Tenant && !u.IsDeleted
                        && _db.UserRoles.Any(ur => ur.UserId == u.UserId
                            && _db.Roles.Any(r => r.RoleId == ur.RoleId && r.NormalizedName == "CUSTOMER")));

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(u => (u.FullName != null && u.FullName.Contains(s))
                             || (u.Email != null && u.Email.Contains(s))
                             || (u.PhoneNumber != null && u.PhoneNumber.Contains(s)));
        }

        if (withOrders)
            q = q.Where(u => _db.Orders.Any(o => o.UserId == u.UserId && SoldStatuses.Contains(o.Status)));

        var total = await q.LongCountAsync(ct);
        var items = await q
            .OrderByDescending(u => u.UserId)
            .Skip((p - 1) * size).Take(size)
            .Select(u => new AdminCustomerDto(
                u.UserId, u.FullName, u.Email, u.PhoneNumber, u.IsActive,
                _db.Orders.Count(o => o.UserId == u.UserId && SoldStatuses.Contains(o.Status)),
                // Only settled orders count towards spend — a pending order is not money taken.
                _db.Orders.Where(o => o.UserId == u.UserId && SoldStatuses.Contains(o.Status))
                    .Sum(o => (decimal?)o.TotalAmount) ?? 0m,
                _db.Orders.Where(o => o.UserId == u.UserId && SoldStatuses.Contains(o.Status))
                    .Max(o => (DateTime?)o.PlacedAt),
                u.CreatedAt))
            .ToListAsync(ct);

        return Ok(ApiResponse<PagedResult<AdminCustomerDto>>.Ok(
            new PagedResult<AdminCustomerDto> { Items = items, Page = p, PageSize = size, TotalCount = total }));
    }
}
