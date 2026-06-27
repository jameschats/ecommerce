using ecomm.api.Common.Models;
using ecomm.api.Features.Auth.Dtos;
using ecomm.api.Features.Auth.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.Auth;

/// <summary>Admin: enable/disable auth providers and toggle self-registration.</summary>
[ApiController]
[Route("api/admin/auth-providers")]
[Authorize(Roles = "Admin")]
public sealed class AuthProvidersAdminController : ControllerBase
{
    private const long TenantId = 1;

    private readonly IAuthProviderService _providers;

    public AuthProvidersAdminController(IAuthProviderService providers) => _providers = providers;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var items = await _providers.GetAllAsync(TenantId, ct);
        var dtos = items.Select(ToDto).ToList();
        return Ok(ApiResponse<IReadOnlyList<AuthProviderDto>>.Ok(dtos));
    }

    [HttpPut("{provider}")]
    public async Task<IActionResult> Update(string provider, UpdateAuthProviderRequest request, CancellationToken ct)
    {
        var updated = await _providers.UpdateAsync(
            TenantId, provider, request.IsEnabled, request.AllowRegistration, request.DisplayOrder, request.ClientId, ct);

        if (updated is null)
            return NotFound(ApiResponse<object>.Fail($"Provider '{provider}' not found."));

        return Ok(ApiResponse<AuthProviderDto>.Ok(ToDto(updated), "Provider updated."));
    }

    private AuthProviderDto ToDto(Data.Entities.AuthProvider p) =>
        new(p.Provider, p.DisplayName, p.IsEnabled, p.AllowRegistration, p.DisplayOrder,
            _providers.GetConfigValue(p, "clientId"));
}
