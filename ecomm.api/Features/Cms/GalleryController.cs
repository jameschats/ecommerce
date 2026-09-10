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
    // A gallery strip tile renders at a few hundred CSS pixels wide (w-80 = 320px); 800px
    // stays sharp on a 2x-retina screen while being far smaller than a typical phone-camera
    // original (often 3000px+ wide), which is most of what makes the gallery slow to load.
    private const int MaxImageWidth = 800;
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
        var resized = _watermark.ResizeIfLarger(ms.ToArray(), type, MaxImageWidth);
        var watermarked = _watermark.Apply(resized, type);
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

    /// <summary>One-time: shrinks every gallery photo already stored at full upload resolution,
    /// from before uploads were resized. Idempotent — a photo already this size or smaller is
    /// left alone.</summary>
    [HttpPost("backfill-resize")]
    public async Task<IActionResult> BackfillResize(CancellationToken ct)
    {
        var result = await _gallery.BackfillResizeAsync(MaxImageWidth, ct);
        return Ok(ApiResponse<Media.BackfillResizeResult>.Ok(
            result, $"Resized {result.Resized} of {result.Candidates} photo(s)"
                    + $" ({FormatBytes(result.BytesBefore)} → {FormatBytes(result.BytesAfter)})."
                    + (result.Failed > 0 ? $" {result.Failed} failed." : "")));
    }

    private static string FormatBytes(long bytes) =>
        bytes >= 1024 * 1024 ? $"{bytes / 1024.0 / 1024.0:0.#} MB" : $"{bytes / 1024.0:0.#} KB";
}
