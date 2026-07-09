using ecomm.api.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.Customers;

/// <summary>Merchant-admin Customers: list/detail/create/update + prebuilt segments.</summary>
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/customers")]
public sealed class CustomerAdminController(ICustomerAdminService customers) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? search, [FromQuery] string? segment,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(ApiResponse<PagedResult<CustomerListItem>>.Ok(await customers.ListAsync(search, segment, page, pageSize, ct)));

    [HttpGet("segments")]
    public async Task<IActionResult> Segments(CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<SegmentDto>>.Ok(await customers.SegmentsAsync(ct)));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct)
        => Ok(ApiResponse<CustomerDetailDto>.Ok(await customers.GetAsync(id, ct)));

    [HttpPost]
    public async Task<IActionResult> Create(CreateCustomerRequest req, CancellationToken ct)
        => Ok(ApiResponse<CustomerDetailDto>.Ok(await customers.CreateAsync(req, ct), "Customer added."));

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, UpdateCustomerRequest req, CancellationToken ct)
        => Ok(ApiResponse<CustomerDetailDto>.Ok(await customers.UpdateAsync(id, req, ct), "Customer saved."));
}
