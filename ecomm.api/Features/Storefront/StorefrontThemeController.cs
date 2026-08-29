using ecomm.api.Common.Models;
using ecomm.api.Data.Context;
using ecomm.api.Features.Cms.SectionTypes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Storefront;

/// <summary>
/// Public storefront theme reads (S1): the published theme's global settings + shared zones,
/// and each page-type template's section list. Anonymous + tenant-scoped by the middleware.
/// </summary>
[ApiController]
[Route("api/storefront")]
public sealed class StorefrontThemeController(IStorefrontThemeService themes, EcommerceDbContext db) : ControllerBase
{
    [HttpGet("theme")]
    public async Task<IActionResult> Theme([FromQuery] string? preview, CancellationToken ct)
        => Ok(ApiResponse<ThemeBundleDto>.Ok(await themes.GetPublishedBundleAsync(preview, ct)));

    [HttpGet("template/{key}")]
    public async Task<IActionResult> Template(string key, [FromQuery] string? preview, CancellationToken ct)
        => Ok(ApiResponse<ThemeTemplateDto>.Ok(await themes.GetTemplateAsync(key, preview, ct)));

    /// <summary>The platform's section-type catalog (kinds/scope/settings schema) — drives the builder + validation.</summary>
    [HttpGet("section-types")]
    public async Task<IActionResult> SectionTypes([FromQuery] string? template, CancellationToken ct)
    {
        var list = string.IsNullOrWhiteSpace(template) ? SectionTypeRegistry.All : SectionTypeRegistry.ForTemplate(template);

        // App-provided section types (S5) are only offered to stores that installed the owning app.
        if (list.Any(s => s.AppSlug is not null))
        {
            var installed = (await (from i in db.AppInstallations
                                    join a in db.Apps on i.AppId equals a.AppId
                                    where i.Status == "installed"
                                    select a.Slug).ToListAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            list = list.Where(s => s.AppSlug is null || installed.Contains(s.AppSlug)).ToList();
        }
        return Ok(ApiResponse<object>.Ok(new { templateKeys = SectionTypeRegistry.TemplateKeys, sectionTypes = list }));
    }
}
