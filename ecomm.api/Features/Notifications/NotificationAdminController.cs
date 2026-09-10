using ecomm.api.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.Notifications;

/// <summary>Merchant management of transactional notification templates + sender identity.</summary>
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/notification-templates")]
public sealed class NotificationAdminController(INotificationAdminService svc) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<NotificationTemplateDto>>.Ok(await svc.ListTemplatesAsync(ct)));

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, UpdateNotificationTemplateRequest req, CancellationToken ct)
        => Ok(ApiResponse<NotificationTemplateDto>.Ok(await svc.UpdateTemplateAsync(id, req, ct), "Template saved."));

    [HttpGet("sender")]
    public async Task<IActionResult> GetSender(CancellationToken ct)
        => Ok(ApiResponse<NotificationSenderDto>.Ok(await svc.GetSenderAsync(ct)));

    [HttpPut("sender")]
    public async Task<IActionResult> UpdateSender(NotificationSenderDto req, CancellationToken ct)
        => Ok(ApiResponse<NotificationSenderDto>.Ok(await svc.UpdateSenderAsync(req, ct), "Sender identity saved."));

    [HttpGet("channels")]
    public async Task<IActionResult> GetChannels(CancellationToken ct)
        => Ok(ApiResponse<ChannelTogglesDto>.Ok(await svc.GetChannelTogglesAsync(ct)));

    [HttpPut("channels")]
    public async Task<IActionResult> UpdateChannels(ChannelTogglesDto req, CancellationToken ct)
        => Ok(ApiResponse<ChannelTogglesDto>.Ok(await svc.UpdateChannelTogglesAsync(req, ct), "Channel settings saved."));
}
