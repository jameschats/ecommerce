using System.Security.Claims;
using System.Text;
using ecomm.api.Common.Models;
using ecomm.api.Features.Customers;
using ecomm.api.Features.Plans;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.Growth;

/// <summary>
/// AI Growth — marketing content generation (G1). Gated behind the <c>growth</c> plan feature;
/// a merchant without it gets 402 and the UI shows an upgrade card instead of the tools.
/// </summary>
[ApiController]
[Authorize(Roles = "Admin")]
[RequiresFeature("growth")]
[Route("api/admin/growth")]
public sealed class GrowthController(
    IGrowthGenerationService gen, IBrandKitService brandKit, IGrowthCampaignService campaigns,
    IGrowthImageService images, IGrowthCampaignSendService sends, IGrowthBulkService bulk,
    ICustomerAdminService customers, IGrowthCalendarService calendar) : ControllerBase
{
    private long? UserId =>
        long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id) ? id : null;

    [HttpGet("types")]
    public IActionResult Types() => Ok(ApiResponse<IReadOnlyList<GrowthTypeDto>>.Ok(gen.Types()));

    [HttpGet("brand-kit")]
    public async Task<IActionResult> GetBrandKit(CancellationToken ct)
        => Ok(ApiResponse<BrandKitDto>.Ok(await brandKit.GetAsync(ct)));

    [HttpPut("brand-kit")]
    public async Task<IActionResult> SaveBrandKit(BrandKitDto request, CancellationToken ct)
        => Ok(ApiResponse<BrandKitDto>.Ok(await brandKit.SaveAsync(request, ct), "Brand kit saved."));

    [HttpPost("generate")]
    public async Task<IActionResult> Generate(GenerateRequest request, CancellationToken ct)
        => Ok(ApiResponse<GrowthContentDto>.Ok(await gen.GenerateAsync(request, UserId, ct: ct), "Generated."));

    [HttpGet("content")]
    public async Task<IActionResult> Library(
        [FromQuery] string? contentType, [FromQuery] long? productId, [FromQuery] long? campaignId,
        [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(ApiResponse<PagedResult<GrowthContentDto>>.Ok(await gen.LibraryAsync(contentType, productId, campaignId, from, to, page, pageSize, ct)));

    [HttpPut("content/{id:long}")]
    public async Task<IActionResult> Update(long id, UpdateContentRequest request, CancellationToken ct)
        => Ok(ApiResponse<GrowthContentDto>.Ok(await gen.UpdateAsync(id, request.Body, request.Title, request.Status, ct), "Saved."));

    [HttpDelete("content/{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await gen.DeleteAsync(id, ct);
        return Ok(ApiResponse<object>.Ok(new { }, "Removed."));
    }

    // ---- Campaigns (G2) ----

    [HttpGet("goals")]
    public IActionResult Goals() => Ok(ApiResponse<IReadOnlyList<GoalDto>>.Ok(campaigns.Goals()));

    [HttpPost("campaigns")]
    public async Task<IActionResult> CreateCampaign(CreateCampaignRequest request, CancellationToken ct)
        => Ok(ApiResponse<CampaignDto>.Ok(await campaigns.CreateAsync(request, UserId, ct), "Campaign generated."));

    [HttpGet("campaigns")]
    public async Task<IActionResult> Campaigns([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(ApiResponse<PagedResult<CampaignSummaryDto>>.Ok(await campaigns.ListAsync(page, pageSize, ct)));

    [HttpGet("campaigns/{id:long}")]
    public async Task<IActionResult> Campaign(long id, CancellationToken ct)
        => Ok(ApiResponse<CampaignDto>.Ok(await campaigns.GetAsync(id, ct)));

    [HttpDelete("campaigns/{id:long}")]
    public async Task<IActionResult> DeleteCampaign(long id, CancellationToken ct)
    {
        await campaigns.DeleteAsync(id, ct);
        return Ok(ApiResponse<object>.Ok(new { }, "Campaign removed."));
    }

    // ---- Campaign sends (M1): email a campaign to a customer segment, now or scheduled ----

    /// <summary>The customer segments a campaign can be sent to, with live counts.</summary>
    [HttpGet("segments")]
    public async Task<IActionResult> Segments(CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<SegmentDto>>.Ok(await customers.SegmentsAsync(ct)));

    /// <summary>Preview a send: how many consented recipients a segment has and whether email copy exists.</summary>
    [HttpGet("campaigns/{id:long}/send")]
    public async Task<IActionResult> SendPreview(long id, [FromQuery] string? segment, CancellationToken ct)
        => Ok(ApiResponse<CampaignSendStatusDto>.Ok(await sends.PreviewAsync(id, segment, ct)));

    [HttpPost("campaigns/{id:long}/send")]
    public async Task<IActionResult> Send(long id, SendCampaignRequest request, CancellationToken ct)
    {
        var status = await sends.ScheduleAsync(id, request, ct);
        var msg = status.ScheduledAt > DateTime.UtcNow ? "Campaign scheduled." : "Campaign send started.";
        return Ok(ApiResponse<CampaignSendStatusDto>.Ok(status, msg));
    }

    [HttpPost("campaigns/{id:long}/cancel-send")]
    public async Task<IActionResult> CancelSend(long id, CancellationToken ct)
        => Ok(ApiResponse<CampaignSendStatusDto>.Ok(await sends.CancelAsync(id, ct), "Scheduled send cancelled."));

    /// <summary>Download a copy-paste pack of every channel's copy — for channels we don't send to directly.</summary>
    [HttpGet("campaigns/{id:long}/export")]
    public async Task<IActionResult> ExportCampaign(long id, CancellationToken ct)
    {
        var c = await campaigns.GetAsync(id, ct);
        var sb = new StringBuilder();
        sb.Append("# ").Append(c.Name).Append("\n\n");
        sb.Append("_Goal: ").Append(c.Goal).Append(" · Language: ").Append(c.Language).Append("_\n\n");
        foreach (var ch in c.Channels)
        {
            sb.Append("\n## ").Append(ChannelLabel(ch.Channel)).Append("\n\n");
            if (ch.Content is null) { sb.Append("_(not generated: ").Append(ch.Error ?? "n/a").Append(")_\n"); continue; }
            if (!string.IsNullOrWhiteSpace(ch.Content.Title)) sb.Append("**Subject:** ").Append(ch.Content.Title).Append("\n\n");
            sb.Append(ch.Content.Body).Append('\n');
        }
        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var slug = new string((c.Name ?? "campaign").Select(ch => char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '-').ToArray());
        return File(bytes, "text/markdown", $"campaign-{slug}.md");
    }

    private static string ChannelLabel(string key) => key switch
    {
        "instagram-caption" => "Instagram caption",
        "facebook-post" => "Facebook post",
        "whatsapp" => "WhatsApp broadcast",
        "email" => "Email campaign",
        _ => key,
    };

    // ---- Marketing calendar (M2 / G3) ----

    /// <summary>Upcoming festivals/occasions within a window (default 120 days), for the lead-time nudges.</summary>
    [HttpGet("festivals")]
    public async Task<IActionResult> Festivals([FromQuery] int withinDays = 120, CancellationToken ct = default)
        => Ok(ApiResponse<IReadOnlyList<FestivalDto>>.Ok(await calendar.UpcomingFestivalsAsync(withinDays, ct)));

    /// <summary>Festivals + this store's scheduled/sent campaigns between two dates (the month grid).</summary>
    [HttpGet("calendar")]
    public async Task<IActionResult> Calendar([FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken ct = default)
        => Ok(ApiResponse<IReadOnlyList<CalendarEntryDto>>.Ok(await calendar.CalendarAsync(from, to, ct)));

    // ---- Bulk generation (M1): one content type across many products, async ----

    [HttpPost("bulk")]
    public async Task<IActionResult> Bulk(BulkGenerateRequest request, CancellationToken ct)
    {
        var result = await bulk.QueueAsync(request, UserId, ct);
        return Ok(ApiResponse<BulkJobDto>.Ok(result, result.Message));
    }

    // ---- Image generation (POC) ----

    [HttpGet("image/styles")]
    public IActionResult ImageStyles() => Ok(ApiResponse<IReadOnlyList<ImageStyleDto>>.Ok(images.Styles()));

    [HttpGet("image/formats")]
    public IActionResult ImageFormats() => Ok(ApiResponse<IReadOnlyList<ImageFormatDto>>.Ok(images.Formats()));

    [HttpPost("image")]
    public async Task<IActionResult> GenerateImage(GenerateImageRequest request, CancellationToken ct)
        => Ok(ApiResponse<GeneratedImageDto>.Ok(await images.GenerateAsync(request, UserId, ct), "Image generated."));

    [HttpGet("image/recent")]
    public async Task<IActionResult> RecentImages(CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<GeneratedImageDto>>.Ok(await images.RecentAsync(ct)));
}

public sealed record UpdateContentRequest(string Body, string? Title, string Status);
