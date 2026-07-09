using ecomm.api.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.Payments;

/// <summary>Merchant-admin Payments settings: provider, per-tenant Razorpay keys, COD.</summary>
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/payments")]
public sealed class PaymentSettingsController(IPaymentSettingsService payments) : ControllerBase
{
    [HttpGet("settings")]
    public async Task<IActionResult> Get(CancellationToken ct)
        => Ok(ApiResponse<PaymentSettingsDto>.Ok(await payments.GetAsync(ct)));

    [HttpPut("settings")]
    public async Task<IActionResult> Update(UpdatePaymentSettingsRequest req, CancellationToken ct)
        => Ok(ApiResponse<PaymentSettingsDto>.Ok(await payments.UpdateAsync(req, ct), "Payment settings saved."));
}
