using System.Security.Claims;
using ecomm.api.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.Reviews;

/// <summary>Public: approved reviews + rating summary for a product.</summary>
[ApiController]
[Route("api/catalog/products/{productId:long}/reviews")]
public sealed class ProductReviewsController : ControllerBase
{
    private readonly IReviewService _reviews;
    public ProductReviewsController(IReviewService reviews) => _reviews = reviews;

    [HttpGet]
    public async Task<IActionResult> Get(long productId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken ct = default)
        => Ok(ApiResponse<ProductReviewsDto>.Ok(await _reviews.GetForProductAsync(productId, page, pageSize, ct)));
}

/// <summary>Customer: submit (or update) a review for a product they can review.</summary>
[ApiController]
[Route("api/reviews")]
[Authorize]
public sealed class ReviewController : ControllerBase
{
    private readonly IReviewService _reviews;
    public ReviewController(IReviewService reviews) => _reviews = reviews;

    private long CurrentUserId =>
        long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id) ? id : 0;

    [HttpGet("eligibility/{productId:long}")]
    public async Task<IActionResult> Eligibility(long productId, CancellationToken ct)
        => Ok(ApiResponse<ReviewEligibilityDto>.Ok(await _reviews.EligibilityAsync(CurrentUserId, productId, ct)));

    [HttpPost]
    public async Task<IActionResult> Submit(SubmitReviewRequest req, CancellationToken ct)
        => Ok(ApiResponse<ReviewDto>.Ok(await _reviews.SubmitAsync(CurrentUserId, req, ct), "Thanks! Your review will appear once approved."));
}

/// <summary>Admin: moderate reviews.</summary>
[ApiController]
[Route("api/admin/reviews")]
[Authorize(Policy = ecomm.api.Common.Security.Perm.ReviewModerate)]
public sealed class ReviewAdminController : ControllerBase
{
    private readonly IReviewService _reviews;
    public ReviewAdminController(IReviewService reviews) => _reviews = reviews;

    public sealed record ApproveRequest(bool Approved);

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(ApiResponse<PagedResult<AdminReviewDto>>.Ok(await _reviews.ListAdminAsync(status, page, pageSize, ct)));

    [HttpPost("{id:long}/approve")]
    public async Task<IActionResult> Approve(long id, ApproveRequest req, CancellationToken ct)
    {
        await _reviews.ApproveAsync(id, req.Approved, ct);
        return Ok(ApiResponse<object>.Ok(null!, req.Approved ? "Review approved." : "Review unapproved."));
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await _reviews.DeleteAsync(id, ct);
        return Ok(ApiResponse<object>.Ok(null!, "Review deleted."));
    }
}
