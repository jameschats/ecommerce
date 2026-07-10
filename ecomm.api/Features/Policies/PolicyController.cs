using ecomm.api.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.Policies;

/// <summary>Merchant-admin store policies (legal pages).</summary>
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/policies")]
public sealed class PolicyAdminController(IPolicyService policies) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<PolicyDto>>.Ok(await policies.ListAsync(ct)));

    [HttpGet("{handle}")]
    public async Task<IActionResult> Get(string handle, CancellationToken ct)
        => Ok(ApiResponse<PolicyDto>.Ok(await policies.GetAsync(handle, ct)));

    [HttpPut("{handle}")]
    public async Task<IActionResult> Save(string handle, SavePolicyRequest req, CancellationToken ct)
        => Ok(ApiResponse<PolicyDto>.Ok(await policies.SaveAsync(handle, req, ct), "Policy saved."));
}

/// <summary>Public: a policy by handle + the footer link list.</summary>
[ApiController]
[Route("api/catalog/policies")]
public sealed class PolicyPublicController(IPolicyService policies) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Links(CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<PolicyLinkDto>>.Ok(await policies.PublicLinksAsync(ct)));

    [HttpGet("{handle}")]
    public async Task<IActionResult> Get(string handle, CancellationToken ct)
    {
        var p = await policies.GetPublicAsync(handle, ct);
        return p is null ? NotFound(ApiResponse<object>.Fail("Policy not found.")) : Ok(ApiResponse<PolicyDto>.Ok(p));
    }
}
