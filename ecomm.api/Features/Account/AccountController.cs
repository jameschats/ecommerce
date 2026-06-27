using System.Security.Claims;
using ecomm.api.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.Account;

[ApiController]
[Authorize]
[Route("api/account")]
public class AccountController : ControllerBase
{
    private readonly IAccountService _account;
    public AccountController(IAccountService account) => _account = account;

    private long CurrentUserId =>
        long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id) ? id : 0;

    // ----- Profile -----
    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile(CancellationToken ct)
    {
        var dto = await _account.GetProfileAsync(CurrentUserId, ct);
        return dto is null ? NotFound(ApiResponse<object>.Fail("User not found.")) : Ok(ApiResponse<ProfileDto>.Ok(dto));
    }

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile(UpdateProfileRequest request, CancellationToken ct)
    {
        var dto = await _account.UpdateProfileAsync(CurrentUserId, request, ct);
        return dto is null ? NotFound(ApiResponse<object>.Fail("User not found.")) : Ok(ApiResponse<ProfileDto>.Ok(dto, "Profile updated."));
    }

    // ----- Addresses -----
    [HttpGet("addresses")]
    public async Task<IActionResult> ListAddresses(CancellationToken ct)
        => Ok(ApiResponse<List<AddressDto>>.Ok(await _account.ListAddressesAsync(CurrentUserId, ct)));

    [HttpPost("addresses")]
    public async Task<IActionResult> CreateAddress(SaveAddressRequest request, CancellationToken ct)
        => Ok(ApiResponse<AddressDto>.Ok(await _account.CreateAddressAsync(CurrentUserId, request, ct), "Address added."));

    [HttpPut("addresses/{id:long}")]
    public async Task<IActionResult> UpdateAddress(long id, SaveAddressRequest request, CancellationToken ct)
    {
        var dto = await _account.UpdateAddressAsync(CurrentUserId, id, request, ct);
        return dto is null ? NotFound(ApiResponse<object>.Fail("Address not found.")) : Ok(ApiResponse<AddressDto>.Ok(dto, "Address updated."));
    }

    [HttpDelete("addresses/{id:long}")]
    public async Task<IActionResult> DeleteAddress(long id, CancellationToken ct)
    {
        var ok = await _account.DeleteAddressAsync(CurrentUserId, id, ct);
        return ok ? Ok(ApiResponse<object>.Ok(null!, "Address removed.")) : NotFound(ApiResponse<object>.Fail("Address not found."));
    }

    [HttpPost("addresses/{id:long}/default")]
    public async Task<IActionResult> SetDefault(long id, CancellationToken ct)
    {
        var ok = await _account.SetDefaultAddressAsync(CurrentUserId, id, ct);
        return ok ? Ok(ApiResponse<object>.Ok(null!, "Default address set.")) : NotFound(ApiResponse<object>.Fail("Address not found."));
    }
}
