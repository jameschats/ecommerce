using ecomm.api.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace ecomm.api.Features.Cms;

/// <summary>Public: visible home-page sections in order (storefront renders from these).</summary>
[ApiController]
[Route("api/cms")]
public sealed class CmsController : ControllerBase
{
    private readonly ICmsService _cms;

    public CmsController(ICmsService cms) => _cms = cms;

    [OutputCache(PolicyName = "public")]
    [HttpGet("home")]
    public async Task<IActionResult> Home(CancellationToken ct)
        => Ok(ApiResponse<List<SectionDto>>.Ok(await _cms.GetHomeSectionsAsync(visibleOnly: true, ct)));
}

/// <summary>Admin: all home sections + reorder / show-hide / retitle.</summary>
[ApiController]
[Route("api/admin/cms")]
[Authorize(Policy = ecomm.api.Common.Security.Perm.CmsManage)]
public sealed class CmsAdminController : ControllerBase
{
    private readonly ICmsService _cms;

    public CmsAdminController(ICmsService cms) => _cms = cms;

    [HttpGet("home")]
    public async Task<IActionResult> Home(CancellationToken ct)
        => Ok(ApiResponse<List<SectionDto>>.Ok(await _cms.GetHomeSectionsAsync(visibleOnly: false, ct)));

    [HttpPut("home")]
    public async Task<IActionResult> Update(UpdateSectionsRequest request, CancellationToken ct)
        => Ok(ApiResponse<List<SectionDto>>.Ok(await _cms.UpdateHomeSectionsAsync(request.Sections ?? [], ct), "Home layout updated."));
}
