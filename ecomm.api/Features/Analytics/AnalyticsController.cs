using System.Globalization;
using ecomm.api.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.Analytics;

/// <summary>Admin analytics: business-pulse widget + profit/margin reports (all reports take ?from=&to=).</summary>
[ApiController]
[Route("api/admin/analytics")]
[Authorize(Roles = "Admin")]
public sealed class AnalyticsController : ControllerBase
{
    private readonly IAnalyticsService _analytics;
    public AnalyticsController(IAnalyticsService analytics) => _analytics = analytics;

    // Parse yyyy-MM-dd; default to the last 30 days. `to` is inclusive (end of day).
    private static (DateTime from, DateTime to) Range(string? from, string? to)
    {
        var toDate = TryDate(to) ?? DateTime.UtcNow.Date;
        var fromDate = TryDate(from) ?? toDate.AddDays(-29);
        return (fromDate.Date, toDate.Date.AddDays(1).AddTicks(-1));
    }

    private static DateTime? TryDate(string? s) =>
        DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var d) ? d : null;

    [HttpGet("summary")]
    public async Task<IActionResult> Summary(CancellationToken ct)
        => Ok(ApiResponse<AnalyticsSummaryDto>.Ok(await _analytics.SummaryAsync(ct)));

    /// <summary>Sales grouped by time — ?bucket=day|month|year, defaulting to month.</summary>
    [HttpGet("sales-over-time")]
    public async Task<IActionResult> SalesOverTime(
        [FromQuery] string? from, [FromQuery] string? to, [FromQuery] string? bucket, CancellationToken ct)
    {
        var (f, t) = Range(from, to);
        return Ok(ApiResponse<List<SalesPeriodRow>>.Ok(
            await _analytics.SalesOverTimeAsync(f, t, bucket ?? "month", ct)));
    }

    [HttpGet("best-sellers")]
    public async Task<IActionResult> BestSellers([FromQuery] string? from, [FromQuery] string? to, CancellationToken ct)
    {
        var (f, t) = Range(from, to);
        return Ok(ApiResponse<List<ProductReportRow>>.Ok(await _analytics.BestSellersAsync(f, t, ct)));
    }

    [HttpGet("margins")]
    public async Task<IActionResult> Margins([FromQuery] string? from, [FromQuery] string? to, [FromQuery] string? order, CancellationToken ct)
    {
        var (f, t) = Range(from, to);
        return Ok(ApiResponse<List<ProductReportRow>>.Ok(await _analytics.MarginsAsync(f, t, order == "low", ct)));
    }

    [HttpGet("return-rate")]
    public async Task<IActionResult> ReturnRate([FromQuery] string? from, [FromQuery] string? to, CancellationToken ct)
    {
        var (f, t) = Range(from, to);
        return Ok(ApiResponse<List<ReturnRateRow>>.Ok(await _analytics.ReturnRateAsync(f, t, ct)));
    }

    [HttpGet("profit-by-category")]
    public async Task<IActionResult> ProfitByCategory([FromQuery] string? from, [FromQuery] string? to, CancellationToken ct)
    {
        var (f, t) = Range(from, to);
        return Ok(ApiResponse<List<GroupProfitRow>>.Ok(await _analytics.ProfitByCategoryAsync(f, t, ct)));
    }

    [HttpGet("profit-by-supplier")]
    public async Task<IActionResult> ProfitBySupplier([FromQuery] string? from, [FromQuery] string? to, CancellationToken ct)
    {
        var (f, t) = Range(from, to);
        return Ok(ApiResponse<List<GroupProfitRow>>.Ok(await _analytics.ProfitBySupplierAsync(f, t, ct)));
    }
}
