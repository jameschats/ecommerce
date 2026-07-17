using System.Security.Claims;
using System.Text.Json;
using ecomm.api.Common.Exceptions;
using ecomm.api.Common.Models;
using ecomm.api.Features.Catalog.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.api.Features.Ai;

/// <summary>Merchant: import products from any spreadsheet with AI column mapping (AI-3).</summary>
[ApiController]
[Route("api/admin/ai/import")]
[Authorize(Roles = "Admin")]
public sealed class AiImportController(IAiImportService svc) : ControllerBase
{
    /// <summary>Upload a file → AI proposes a column → field mapping (metered) for the merchant to review.</summary>
    [HttpPost("analyze")]
    public async Task<IActionResult> Analyze(IFormFile file, [FromForm] string? format, CancellationToken ct)
    {
        using var ms = await BufferAsync(file, ct);
        return Ok(ApiResponse<AiImportAnalysis>.Ok(await svc.AnalyzeAsync(ms, file.FileName, format, ct)));
    }

    /// <summary>Re-upload the file + confirmed mapping → transform and import (auto-creates missing categories).</summary>
    [HttpPost("apply")]
    public async Task<IActionResult> Apply(IFormFile file, [FromForm] string mapping, CancellationToken ct)
    {
        Dictionary<string, string> map;
        try { map = JsonSerializer.Deserialize<Dictionary<string, string>>(mapping ?? "{}") ?? new(); }
        catch (JsonException) { throw new AppException("Invalid mapping.", 400); }

        var userId = long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : (long?)null;
        using var ms = await BufferAsync(file, ct);
        return Ok(ApiResponse<ImportResultDto>.Ok(await svc.ApplyAsync(ms, file.FileName, map, userId, ct)));
    }

    private static async Task<MemoryStream> BufferAsync(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) throw new AppException("Please choose a file.", 400);
        var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        ms.Position = 0;
        return ms;
    }
}
