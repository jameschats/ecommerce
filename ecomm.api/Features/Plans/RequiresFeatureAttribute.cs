using ecomm.api.Common.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace ecomm.api.Features.Plans;

/// <summary>
/// Gates an action behind a plan feature. Returns 402 (with an upgrade message) when the tenant's
/// plan doesn't include the key — the storefront never sees this; it's for merchant-admin actions
/// sold as paid add-ons. Fail-closed: no plan means no feature.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequiresFeatureAttribute(string featureKey) : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var entitlements = context.HttpContext.RequestServices.GetRequiredService<IEntitlementService>();
        if (!await entitlements.HasFeatureAsync(featureKey, context.HttpContext.RequestAborted))
            throw new AppException(
                "This feature isn't included in your current plan. Upgrade to unlock it.",
                StatusCodes.Status402PaymentRequired);

        await next();
    }
}
