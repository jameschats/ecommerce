using System.Security.Claims;
using ecomm.api.Common.Models;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Common.Middleware;

/// <summary>
/// Enforces per-tenant staff access levels on the merchant admin (<c>/api/admin/*</c>):
/// <list type="bullet">
///   <item>a <b>Disabled</b> staff member is blocked from the admin entirely (their token may still be valid);</item>
///   <item>a <b>Viewer</b> is blocked from any mutating request (read-only).</item>
/// </list>
/// Users with no <see cref="TenantStaff"/> row (legacy admins, super-admins) are unaffected.
/// Runs after authentication + tenant resolution so the DB lookup is tenant-scoped.
/// </summary>
public sealed class StaffAccessGuardMiddleware(RequestDelegate next)
{
    public async Task Invoke(HttpContext context, EcommerceDbContext db)
    {
        if (context.User.Identity?.IsAuthenticated == true
            && context.Request.Path.StartsWithSegments("/api/admin")
            && long.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.User.FindFirstValue("sub"), out var userId))
        {
            var staff = await db.TenantStaff.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == userId);
            if (staff is not null)
            {
                if (staff.Status == StaffStatus.Disabled)
                { await Deny(context, "Your admin access has been disabled. Contact the store owner."); return; }

                if (staff.AccessLevel == StaffAccess.Viewer && IsMutating(context.Request.Method))
                { await Deny(context, "Your role is read-only. Ask an owner or admin to make changes."); return; }
            }
        }
        await next(context);
    }

    private static async Task Deny(HttpContext context, string message)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(ApiResponse<object>.Fail(message));
    }

    private static bool IsMutating(string method) =>
        !HttpMethods.IsGet(method) && !HttpMethods.IsHead(method) && !HttpMethods.IsOptions(method);
}
