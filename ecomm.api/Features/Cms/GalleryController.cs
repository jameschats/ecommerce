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
    public async Task<IActionResult> List([FromQuery] string section, CancellationToken ct)
        => Ok(ApiResponse<List<GalleryImageDto>>.Ok(await _gallery.GetActiveAsync(section, ct)));

    [OutputCache(PolicyName = "public")]
    [HttpGet("title")]
    public async Task<IActionResult> Title([FromQuery] string section, CancellationToken ct)
        => Ok(ApiResponse<string>.Ok(await _gallery.GetSectionTitleAsync(section, ct)));

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
[Authorize(Policy = ecomm.api.Common.Security.Perm.CmsManage)]
public sealed class GalleryAdminController : ControllerBase
{
    private const long MaxImageBytes = 5 * 1024 * 1024;
    private static readonly string[] AllowedTypes = ["image/jpeg", "image/png", "image/webp", "image/gif"];
    private readonly IGalleryService _gallery;
    private readonly Media.IImageWatermarkService _watermark;

    public GalleryAdminController(IGalleryService gallery, Media.IImageWatermarkService watermark)
    {
        _gallery = gallery;
        _watermark = watermark;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string section, CancellationToken ct)
        => Ok(ApiResponse<List<AdminGalleryImageDto>>.Ok(await _gallery.GetAllAsync(section, ct)));

    [HttpGet("title")]
    public async Task<IActionResult> Title([FromQuery] string section, CancellationToken ct)
        => Ok(ApiResponse<string>.Ok(await _gallery.GetSectionTitleAsync(section, ct)));

    [HttpPut("title")]
    public async Task<IActionResult> SetTitle([FromQuery] string section, [FromBody] SectionTitleUpsert req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Title)) throw new AppException("Enter a title.");
        await _gallery.SetSectionTitleAsync(section, req.Title, ct);
        return Ok(ApiResponse<object>.Ok(null!, "Title saved."));
    }

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
        var watermarked = _watermark.Apply(ms.ToArray(), type);
        await _gallery.SetImageAsync(id, watermarked, type, ct);
        return Ok(ApiResponse<object>.Ok(null!, "Image uploaded."));
    }

    /// <summary>One-time: watermarks every gallery photo already stored from before watermarking existed.</summary>
    [HttpPost("backfill-watermarks")]
    public async Task<IActionResult> BackfillWatermarks(CancellationToken ct)
    {
        var result = await _gallery.BackfillWatermarksAsync(ct);
        return Ok(ApiResponse<Media.BackfillWatermarksResult>.Ok(
            result, $"Watermarked {result.Watermarked} of {result.Candidates} photo(s)."
                    + (result.Failed > 0 ? $" {result.Failed} failed." : "")));
    }

    /// <summary>One-time: restores every gallery photo to its pre-processing backup (full
    /// resolution, watermark re-applied fresh) — undoes a resize backfill that was run and
    /// then reverted.</summary>
    [HttpPost("restore-originals")]
    public async Task<IActionResult> RestoreOriginals(CancellationToken ct)
    {
        var result = await _gallery.RestoreOriginalsAsync(ct);
        return Ok(ApiResponse<Media.RestoreOriginalsResult>.Ok(
            result, $"Restored {result.Restored} of {result.Candidates} photo(s)."
                    + (result.NoBackupFound > 0 ? $" {result.NoBackupFound} had no backup to restore from." : "")
                    + (result.Failed > 0 ? $" {result.Failed} failed." : "")));
    }
}
