using ecomm.api.Common.Exceptions;
using ecomm.api.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace ecomm.api.Features.Cms;

/// <summary>Public: active gallery photos + streaming uploaded gallery images.</summary>
[ApiController]
[Route("api/cms/gallery")]
public sealed class GalleryController : ControllerBase
{
    private readonly IGalleryService _gallery;

    public GalleryController(IGalleryService gallery) => _gallery = gallery;

    [OutputCache(PolicyName = "public")]
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(ApiResponse<List<GalleryImageDto>>.Ok(await _gallery.GetActiveAsync(ct)));

    [HttpGet("{id:long}/image")]
    public async Task<IActionResult> Image(long id, CancellationToken ct)
    {
        var img = await _gallery.GetImageAsync(id, ct);
        if (img is null) return NotFound();
        Response.Headers.CacheControl = "public, max-age=86400";
        return File(img.Value.Data, img.Value.ContentType);
    }
}

/// <summary>Admin: gallery CRUD + image upload.</summary>
[ApiController]
[Route("api/admin/cms/gallery")]
[Authorize(Roles = "Admin")]
public sealed class GalleryAdminController : ControllerBase
{
    private const long MaxImageBytes = 5 * 1024 * 1024;
    private static readonly string[] AllowedTypes = ["image/jpeg", "image/png", "image/webp", "image/gif"];
    private readonly IGalleryService _gallery;

    public GalleryAdminController(IGalleryService gallery) => _gallery = gallery;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(ApiResponse<List<AdminGalleryImageDto>>.Ok(await _gallery.GetAllAsync(ct)));

    [HttpPost]
    public async Task<IActionResult> Create(GalleryImageUpsert req, CancellationToken ct)
        => Ok(ApiResponse<AdminGalleryImageDto>.Ok(await _gallery.CreateAsync(req, ct), "Gallery image added."));

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, GalleryImageUpsert req, CancellationToken ct)
        => Ok(ApiResponse<AdminGalleryImageDto>.Ok(await _gallery.UpdateAsync(id, req, ct), "Gallery image updated."));

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await _gallery.DeleteAsync(id, ct);
        return Ok(ApiResponse<object>.Ok(null!, "Gallery image deleted."));
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
        await _gallery.SetImageAsync(id, ms.ToArray(), type, ct);
        return Ok(ApiResponse<object>.Ok(null!, "Image uploaded."));
    }
}
