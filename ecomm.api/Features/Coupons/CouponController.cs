using ecomm.api.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.Coupons;

/// <summary>Admin: manage discount coupons. (Customers apply codes via the checkout quote.)</summary>
[ApiController]
[Route("api/admin/coupons")]
[Authorize(Policy = ecomm.api.Common.Security.Perm.CouponManage)]
public sealed class CouponAdminController : ControllerBase
{
    private readonly ICouponService _coupons;
    public CouponAdminController(ICouponService coupons) => _coupons = coupons;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(ApiResponse<List<AdminCouponDto>>.Ok(await _coupons.ListAsync(ct)));

    [HttpPost]
    public async Task<IActionResult> Create(SaveCouponRequest req, CancellationToken ct)
        => Ok(ApiResponse<AdminCouponDto>.Ok(await _coupons.CreateAsync(req, ct), "Coupon created."));

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, SaveCouponRequest req, CancellationToken ct)
        => Ok(ApiResponse<AdminCouponDto>.Ok(await _coupons.UpdateAsync(id, req, ct), "Coupon updated."));

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await _coupons.DeleteAsync(id, ct);
        return Ok(ApiResponse<object>.Ok(null!, "Coupon deleted."));
    }
}
