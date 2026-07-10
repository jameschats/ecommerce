using ecomm.api.Common.Models;
using ecomm.api.Features.Cms.SectionTypes;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.Storefront;

/// <summary>
/// Public storefront theme reads (S1): the published theme's global settings + shared zones,
/// and each page-type template's section list. Anonymous + tenant-scoped by the middleware.
/// </summary>
[ApiController]
[Route("api/storefront")]
public sealed class StorefrontThemeController(IStorefrontThemeService themes) : ControllerBase
{
    [HttpGet("theme")]
    public async Task<IActionResult> Theme(CancellationToken ct)
        => Ok(ApiResponse<ThemeBundleDto>.Ok(await themes.GetPublishedBundleAsync(ct)));

    [HttpGet("template/{key}")]
    public async Task<IActionResult> Template(string key, CancellationToken ct)
        => Ok(ApiResponse<ThemeTemplateDto>.Ok(await themes.GetTemplateAsync(key, ct)));

    /// <summary>The platform's section-type catalog (kinds/scope/settings schema) — drives the builder + validation.</summary>
    [HttpGet("section-types")]
    public IActionResult SectionTypes([FromQuery] string? template)
    {
        var list = string.IsNullOrWhiteSpace(template) ? SectionTypeRegistry.All : SectionTypeRegistry.ForTemplate(template);
        return Ok(ApiResponse<object>.Ok(new { templateKeys = SectionTypeRegistry.TemplateKeys, sectionTypes = list }));
    }
}
