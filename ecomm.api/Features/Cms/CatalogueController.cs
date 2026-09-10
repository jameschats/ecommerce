using ecomm.api.Common.Exceptions;
using ecomm.api.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace ecomm.api.Features.Cms;

/// <summary>Public: the downloadable PDF catalogues shown on /catalogues.</summary>
[ApiController]
[Route("api/cms/catalogues")]
public sealed class CatalogueController : ControllerBase
{
    private readonly ICatalogueService _catalogues;
    public CatalogueController(ICatalogueService catalogues) => _catalogues = catalogues;

    [OutputCache(PolicyName = "public", Tags = ["catalogues"])]
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(ApiResponse<List<CatalogueDto>>.Ok(await _catalogues.GetActiveAsync(ct)));
}

/// <summary>Admin: upload/rename/reorder/remove catalogue PDFs.</summary>
[ApiController]
[Route("api/admin/cms/catalogues")]
[Authorize(Policy = ecomm.api.Common.Security.Perm.CmsManage)]
public sealed class CatalogueAdminController : ControllerBase
{
    private const long MaxFileBytes = 25 * 1024 * 1024;
    private readonly ICatalogueService _catalogues;
    private readonly IOutputCacheStore _cache;

    public CatalogueAdminController(ICatalogueService catalogues, IOutputCacheStore cache)
    {
        _catalogues = catalogues;
        _cache = cache;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(ApiResponse<List<CatalogueDto>>.Ok(await _catalogues.GetAllAsync(ct)));

    [HttpPost]
    [RequestSizeLimit(MaxFileBytes + 1024)]
    public async Task<IActionResult> Upload(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) throw new AppException("Please choose a non-empty PDF file.");
        if (file.Length > MaxFileBytes) throw new AppException("File must be 25 MB or smaller.");
        if (!string.Equals(file.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase)
            && !file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            throw new AppException("Only PDF files are allowed.");

        var dto = await _catalogues.UploadAsync(file, ct);
        await _cache.EvictByTagAsync("catalogues", ct);
        return Ok(ApiResponse<CatalogueDto>.Ok(dto, "Catalogue uploaded."));
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, CatalogueUpdateRequest req, CancellationToken ct)
    {
        var dto = await _catalogues.UpdateAsync(id, req, ct);
        await _cache.EvictByTagAsync("catalogues", ct);
        return Ok(ApiResponse<CatalogueDto>.Ok(dto, "Saved."));
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await _catalogues.DeleteAsync(id, ct);
        await _cache.EvictByTagAsync("catalogues", ct);
        return Ok(ApiResponse<object>.Ok(null!, "Catalogue deleted."));
    }
}
