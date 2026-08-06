using System.Security.Claims;
using ecomm.api.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.Media;

/// <summary>Admin: upload an image to media storage; returns the id + public URL to attach to a product.</summary>
[ApiController]
[Route("api/admin/media")]
[Authorize(Policy = ecomm.api.Common.Security.Perm.MediaManage)]
public sealed class MediaController : ControllerBase
{
    private readonly IMediaService _media;

    public MediaController(IMediaService media) => _media = media;

    private long? CurrentUserId =>
        long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id) ? id : null;

    [HttpPost]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> Upload(IFormFile? file, CancellationToken ct)
        => Ok(ApiResponse<MediaDto>.Ok(await _media.UploadAsync(file!, CurrentUserId, ct), "Image uploaded."));
}
