using ecomm.api.Common.Exceptions;
using ecomm.api.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.Customers;

/// <summary>Merchant-admin Customers: list/detail/create/update + prebuilt segments + tags + CSV import.</summary>
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/customers")]
public sealed class CustomerAdminController(ICustomerAdminService customers) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? search, [FromQuery] string? segment, [FromQuery] string? tag,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(ApiResponse<PagedResult<CustomerListItem>>.Ok(await customers.ListAsync(search, segment, tag, page, pageSize, ct)));

    [HttpGet("segments")]
    public async Task<IActionResult> Segments(CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<SegmentDto>>.Ok(await customers.SegmentsAsync(ct)));

    [HttpGet("tags")]
    public async Task<IActionResult> Tags(CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<TagCountDto>>.Ok(await customers.TagsAsync(ct)));

    [HttpPost("import")]
    public async Task<IActionResult> Import(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            throw new AppException("Please upload a non-empty .csv file.");
        if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            throw new AppException("Only .csv files are supported.");

        await using var stream = file.OpenReadStream();
        var result = await customers.ImportAsync(stream, ct);
        return Ok(ApiResponse<CustomerImportResult>.Ok(result,
            $"Imported {result.Created} new, updated {result.Updated}" + (result.Skipped > 0 ? $", skipped {result.Skipped}." : ".")));
    }

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
