using ecomm.api.Common.Models;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.Onboarding;

/// <summary>Public: merchant self-serve store signup.</summary>
[ApiController]
[Route("api/onboarding")]
public sealed class OnboardingController(IOnboardingService onboarding) : ControllerBase
{
    [HttpPost("signup")]
    public async Task<IActionResult> Signup(SignupRequest request, CancellationToken ct)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await onboarding.SignupAsync(request, ip, ct);
        return Ok(ApiResponse<OnboardingResult>.Ok(result, "Store created."));
    }

    [HttpGet("slug-available/{slug}")]
    public async Task<IActionResult> SlugAvailable(string slug, CancellationToken ct)
    {
        // A cheap availability probe for the signup form (best-effort; signup re-validates).
        var available = await onboarding.IsSlugAvailableAsync(slug, ct);
        return Ok(ApiResponse<bool>.Ok(available));
    }

    [HttpGet("suggest-slug")]
    public async Task<IActionResult> SuggestSlug([FromQuery] string? name, CancellationToken ct)
        => Ok(ApiResponse<string>.Ok(await onboarding.SuggestSlugAsync(name, ct)));
}
