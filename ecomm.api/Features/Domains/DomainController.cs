using ecomm.api.Common.Models;
using ecomm.api.Data.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Domains;

/// <summary>Merchant: connect + verify a custom domain for the current store.</summary>
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/domain")]
public sealed class DomainController(IDomainService domains) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
        => Ok(ApiResponse<DomainStatusDto>.Ok(await domains.GetAsync(ct)));

    [HttpPut]
    public async Task<IActionResult> Connect(ConnectDomainRequest req, CancellationToken ct)
        => Ok(ApiResponse<DomainStatusDto>.Ok(await domains.ConnectAsync(req.Domain, ct), "Domain saved — add the DNS record, then verify."));

    [HttpPost("verify")]
    public async Task<IActionResult> Verify(CancellationToken ct)
        => Ok(ApiResponse<DomainStatusDto>.Ok(await domains.VerifyAsync(ct), "Domain verified."));

    [HttpDelete]
    public async Task<IActionResult> Disconnect(CancellationToken ct)
        => Ok(ApiResponse<DomainStatusDto>.Ok(await domains.DisconnectAsync(ct), "Domain disconnected."));
}

/// <summary>
/// Public: serves the current tenant's domain-verification token. Reached by our own verifier
/// over the merchant's custom host once their DNS points at us — proving the domain routes here.
/// </summary>
[ApiController]
[Route(".well-known/wavcommerce-domain-verification")]
public sealed class DomainVerificationController(EcommerceDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Token(CancellationToken ct)
    {
        var token = await db.Tenants.AsNoTracking()
            .Where(t => t.TenantId == db.CurrentTenantId)
            .Select(t => t.CustomDomainToken).FirstOrDefaultAsync(ct);
        return Content(token ?? "", "text/plain");
    }
}
