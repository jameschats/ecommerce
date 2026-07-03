using ecomm.api.Common.Models;
using ecomm.api.Data.Context;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Plans;

/// <summary>Public: the subscription plans a merchant can sign up for (pricing page).</summary>
[ApiController]
[Route("api/plans")]
public sealed class PlansController(EcommerceDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var plans = await db.Plans.AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.DisplayOrder)
            .Select(p => new PlanDto(p.PlanId, p.Name, p.Slug, p.MonthlyPrice, p.MaxProducts, p.MaxOrders, p.AiCredits))
            .ToListAsync(ct);
        return Ok(ApiResponse<IReadOnlyList<PlanDto>>.Ok(plans));
    }
}

public sealed record PlanDto(
    int PlanId, string Name, string Slug, decimal MonthlyPrice,
    int? MaxProducts, int? MaxOrders, int AiCredits);
