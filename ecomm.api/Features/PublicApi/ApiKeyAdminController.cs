using System.Security.Claims;
using ecomm.api.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.PublicApi;

/// <summary>Merchant self-service management of their own public-API keys — regular JWT-authenticated
/// admin surface, distinct from the public API itself (which the keys created here authenticate
/// against, under the "ApiKey" scheme).</summary>
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/api-keys")]
public sealed class ApiKeyAdminController(IApiKeyService keys) : ControllerBase
{
    private long? UserId =>
        long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id) ? id : null;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<ApiKeyDto>>.Ok(await keys.ListAsync(ct)));

    [HttpGet("scopes")]
    public IActionResult Scopes() => Ok(ApiResponse<IReadOnlyList<string>>.Ok(ApiKeyService.ValidScopes));

    [HttpPost]
    public async Task<IActionResult> Create(CreateApiKeyRequest request, CancellationToken ct)
        => Ok(ApiResponse<CreatedApiKeyDto>.Ok(await keys.CreateAsync(request, UserId, ct),
            "Copy this key now — you won't be able to see it again."));

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Revoke(long id, CancellationToken ct)
    {
        await keys.RevokeAsync(id, ct);
        return Ok(ApiResponse<object>.Ok(new { }, "Key revoked."));
    }
}
