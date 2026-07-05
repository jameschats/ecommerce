using ecomm.api.Common.Models;

namespace ecomm.api.Common.Middleware;

/// <summary>
/// When a super-admin is impersonating a store in READ-ONLY ("view") mode, block any
/// mutating request (POST/PUT/PATCH/DELETE). Full-mode impersonation is unrestricted.
/// The impersonation claims are minted by JwtTokenService.CreateImpersonationToken.
/// </summary>
public sealed class ImpersonationGuardMiddleware(RequestDelegate next)
{
    public async Task Invoke(HttpContext context)
    {
        var mode = context.User.FindFirst("imp_mode")?.Value;
        if (mode == "view" && IsMutating(context.Request.Method))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(ApiResponse<object>.Fail("You are viewing this store read-only. Switch to full impersonation to make changes."));
            return;
        }
        await next(context);
    }

    private static bool IsMutating(string method) =>
        !HttpMethods.IsGet(method) && !HttpMethods.IsHead(method) && !HttpMethods.IsOptions(method);
}
