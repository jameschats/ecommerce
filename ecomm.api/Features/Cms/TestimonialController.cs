using ecomm.api.Common.Exceptions;
using ecomm.api.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace ecomm.api.Features.Cms;

/// <summary>Public: active testimonials + streaming uploaded photos.</summary>
[ApiController]
[Route("api/cms/testimonials")]
public sealed class TestimonialController : ControllerBase
{
    private readonly ITestimonialService _testimonials;

    public TestimonialController(ITestimonialService testimonials) => _testimonials = testimonials;

    [OutputCache(PolicyName = "public")]
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(ApiResponse<List<TestimonialDto>>.Ok(await _testimonials.GetActiveAsync(ct)));

    [HttpGet("{id:long}/photo")]
    public async Task<IActionResult> Photo(long id, CancellationToken ct)
    {
        var img = await _testimonials.GetPhotoAsync(id, ct);
        if (img is null) return NotFound();
        Response.Headers.CacheControl = "public, max-age=86400";
        return File(img.Value.Data, img.Value.ContentType);
    }
}

/// <summary>Admin: testimonial CRUD + photo upload.</summary>
[ApiController]
[Route("api/admin/cms/testimonials")]
[Authorize(Policy = ecomm.api.Common.Security.Perm.CmsManage)]
public sealed class TestimonialAdminController : ControllerBase
{
    private const long MaxPhotoBytes = 5 * 1024 * 1024;
    private static readonly string[] AllowedTypes = ["image/jpeg", "image/png", "image/webp", "image/gif"];
    private readonly ITestimonialService _testimonials;

    public TestimonialAdminController(ITestimonialService testimonials) => _testimonials = testimonials;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(ApiResponse<List<AdminTestimonialDto>>.Ok(await _testimonials.GetAllAsync(ct)));

    [HttpPost]
    public async Task<IActionResult> Create(TestimonialUpsert req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Name)) throw new AppException("Enter the customer's name.");
        if (string.IsNullOrWhiteSpace(req.Quote)) throw new AppException("Enter the quote.");
        return Ok(ApiResponse<AdminTestimonialDto>.Ok(await _testimonials.CreateAsync(req, ct), "Testimonial added."));
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, TestimonialUpsert req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Name)) throw new AppException("Enter the customer's name.");
        if (string.IsNullOrWhiteSpace(req.Quote)) throw new AppException("Enter the quote.");
        return Ok(ApiResponse<AdminTestimonialDto>.Ok(await _testimonials.UpdateAsync(id, req, ct), "Testimonial updated."));
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await _testimonials.DeleteAsync(id, ct);
        return Ok(ApiResponse<object>.Ok(null!, "Testimonial deleted."));
    }

    [HttpPost("{id:long}/photo")]
    [RequestSizeLimit(MaxPhotoBytes + 1024)]
    public async Task<IActionResult> Upload(long id, IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) throw new AppException("Please choose a non-empty image file.");
        if (file.Length > MaxPhotoBytes) throw new AppException("Image must be 5 MB or smaller.");
        var type = file.ContentType?.ToLowerInvariant() ?? "";
        if (!AllowedTypes.Contains(type)) throw new AppException("Only JPEG, PNG, WebP or GIF images are allowed.");

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        await _testimonials.SetPhotoAsync(id, ms.ToArray(), type, ct);
        return Ok(ApiResponse<object>.Ok(null!, "Photo uploaded."));
    }
}
