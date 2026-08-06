using ecomm.api.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.Campaigns;

/// <summary>Promotional email campaigns, sent in batches from the admin screen.</summary>
[ApiController]
[Route("api/admin/campaigns")]
[Authorize(Roles = "Admin")]
public sealed class CampaignsAdminController : ControllerBase
{
    private readonly ICampaignService _campaigns;
    public CampaignsAdminController(ICampaignService campaigns) => _campaigns = campaigns;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(ApiResponse<List<CampaignDto>>.Ok(await _campaigns.ListAsync(ct)));

    /// <summary>How many people each audience would reach, shown before anything is sent.</summary>
    [HttpGet("audience")]
    public async Task<IActionResult> Audience(CancellationToken ct)
        => Ok(ApiResponse<AudienceCountDto>.Ok(await _campaigns.AudienceCountsAsync(ct)));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveCampaignRequest req, CancellationToken ct)
        => Ok(ApiResponse<CampaignDto>.Ok(await _campaigns.CreateAsync(req, ct), "Campaign created."));

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] SaveCampaignRequest req, CancellationToken ct)
    {
        var updated = await _campaigns.UpdateAsync(id, req, ct);
        return updated is null
            ? NotFound(ApiResponse<object>.Fail("Campaign not found."))
            : Ok(ApiResponse<CampaignDto>.Ok(updated, "Saved."));
    }

    /// <summary>
    /// Sends the next batch. Called repeatedly by the admin screen until nothing is left,
    /// which keeps each request short and makes an interrupted send resumable — the
    /// recipient rows record exactly who has already been mailed.
    /// </summary>
    [HttpPost("{id:long}/send")]
    public async Task<IActionResult> Send(long id, [FromQuery] int batchSize = 25, CancellationToken ct = default)
    {
        var result = await _campaigns.SendBatchAsync(id, batchSize, ct);
        return Ok(ApiResponse<SendResultDto>.Ok(result,
            result.Remaining > 0
                ? $"Sent {result.Sent}, {result.Remaining} to go."
                : $"Finished — {result.Sent} sent this batch."));
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
        => await _campaigns.DeleteAsync(id, ct)
            ? Ok(ApiResponse<object>.Ok(new { deleted = true }, "Campaign deleted."))
            : NotFound(ApiResponse<object>.Fail("Campaign not found."));
}
