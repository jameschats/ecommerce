using ecomm.api.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.Settings;

[ApiController]
[Authorize(Policy = ecomm.api.Common.Security.Perm.SettingsManage)]
[Route("api/admin/store")]
public class StoreSettingsController : ControllerBase
{
    private readonly IStoreSettingsService _settings;
    public StoreSettingsController(IStoreSettingsService settings) => _settings = settings;

    [HttpGet("settings")]
    public async Task<IActionResult> Get(CancellationToken ct)
        => Ok(ApiResponse<StoreSettingsDto>.Ok(await _settings.GetAsync(ct)));

    [HttpPut("settings")]
    public async Task<IActionResult> Update(UpdateStoreSettingsRequest request, CancellationToken ct)
        => Ok(ApiResponse<StoreSettingsDto>.Ok(await _settings.UpdateAsync(request, ct), "Store settings updated."));
}
