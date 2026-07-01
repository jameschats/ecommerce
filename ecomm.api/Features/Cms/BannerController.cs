using ecomm.api.Common.Exceptions;
using ecomm.api.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.Cms;

/// <summary>Public: active home banners + streaming uploaded banner images.</summary>
[ApiController]
[Route("api/cms/banners")]
public sealed class BannerController : ControllerBase
{
    private readonly IBannerService _banners;

    public BannerController(IBannerService banners) => _banners = banners;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(ApiResponse<List<BannerDto>>.Ok(await _banners.GetActiveAsync(ct)));

    [HttpGet("{id:long}/image")]
    public async Task<IActionResult> Image(long id, CancellationToken ct)
    {
        var img = await _banners.GetImageAsync(id, ct);
        if (img is null) return NotFound();
        Response.Headers.CacheControl = "public, max-age=86400";
        return File(img.Value.Data, img.Value.ContentType);
    }
}

/// <summary>Admin: banner CRUD + image upload.</summary>
[ApiController]
[Route("api/admin/cms/banners")]
[Authorize(Roles = "Admin")]
public sealed class BannerAdminController : ControllerBase
{
    private const long MaxImageBytes = 5 * 1024 * 1024;
    private static readonly string[] AllowedTypes = ["image/jpeg", "image/png", "image/webp", "image/gif"];
    private readonly IBannerService _banners;

    public BannerAdminController(IBannerService banners) => _banners = banners;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(ApiResponse<List<AdminBannerDto>>.Ok(await _banners.GetAllAsync(ct)));

    [HttpPost]
    public async Task<IActionResult> Create(BannerUpsert req, CancellationToken ct)
        => Ok(ApiResponse<AdminBannerDto>.Ok(await _banners.CreateAsync(req, ct), "Banner created."));

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, BannerUpsert req, CancellationToken ct)
        => Ok(ApiResponse<AdminBannerDto>.Ok(await _banners.UpdateAsync(id, req, ct), "Banner updated."));

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await _banners.DeleteAsync(id, ct);
        return Ok(ApiResponse<object>.Ok(null!, "Banner deleted."));
    }

    [HttpPost("{id:long}/image")]
    [RequestSizeLimit(MaxImageBytes + 1024)]
    public async Task<IActionResult> Upload(long id, IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) throw new AppException("Please choose a non-empty image file.");
        if (file.Length > MaxImageBytes) throw new AppException("Image must be 5 MB or smaller.");
        var type = file.ContentType?.ToLowerInvariant() ?? "";
        if (!AllowedTypes.Contains(type)) throw new AppException("Only JPEG, PNG, WebP or GIF images are allowed.");

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        await _banners.SetImageAsync(id, ms.ToArray(), type, ct);
        return Ok(ApiResponse<object>.Ok(null!, "Image uploaded."));
    }
}
