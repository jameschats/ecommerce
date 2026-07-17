using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using ecomm.api.Common;
using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Catalog.Dtos;
using ecomm.api.Features.Catalog.Services;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Ai;

public sealed record AiImportField(string Value, string Label);
public sealed record AiImportAnalysis(
    IReadOnlyList<string> Headers,
    IReadOnlyList<IReadOnlyList<string>> SampleRows,
    IReadOnlyDictionary<string, string> Mapping,
    IReadOnlyList<AiImportField> Fields,
    string? DetectedFormat,
    IReadOnlyList<AiImportField> Formats);

public interface IAiImportService
{
    /// <summary>Read a merchant's arbitrary .xlsx/.csv. Known platform columns (Shopify/Woo/Wix) map via a
    /// preset; anything left over is mapped by the AI (metered only when the AI is actually called). Returns
    /// the merged mapping + sample rows for the merchant to review/correct. <paramref name="format"/> forces a
    /// platform preset (else it's auto-detected).</summary>
    Task<AiImportAnalysis> AnalyzeAsync(Stream stream, string? fileName, string? format = null, CancellationToken ct = default);

    /// <summary>Transform the file per the confirmed mapping, auto-create any missing categories, and import
    /// through the existing product importer. Not metered (the AI spend happened at analyze time).</summary>
    Task<ImportResultDto> ApplyAsync(Stream stream, string? fileName, IReadOnlyDictionary<string, string> mapping, long? userId, CancellationToken ct = default);
}

/// <summary>
/// AI-3 "bring your own file" import. Reuses <see cref="IProductImportService"/> for the actual write —
/// this service only does the AI column-mapping and the transform to our canonical column layout, so a
/// merchant can upload a spreadsheet in any shape and preview/confirm before anything is written.
/// </summary>
public sealed class AiImportService(EcommerceDbContext db, IAiCreditService credits, IAiService ai, IProductImportService importer) : IAiImportService
{
    // target field value → canonical header the importer recognises
    private static readonly IReadOnlyDictionary<string, string> Canonical = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["sku"] = "SKU", ["name"] = "Name", ["category"] = "Category", ["brand"] = "Brand", ["price"] = "Price",
        ["compareatprice"] = "CompareAtPrice", ["costprice"] = "CostPrice", ["hsncode"] = "HsnCode",
        ["status"] = "Status", ["shortdescription"] = "ShortDescription", ["description"] = "Description", ["imageurl"] = "ImageUrl",
    };

    private static readonly IReadOnlyList<AiImportField> Fields = new[]
    {
        new AiImportField("sku", "SKU"), new AiImportField("name", "Name (title)"), new AiImportField("category", "Category"),
        new AiImportField("brand", "Brand"), new AiImportField("price", "Price"), new AiImportField("compareatprice", "Compare-at price"),
        new AiImportField("costprice", "Cost price"), new AiImportField("hsncode", "HSN code"), new AiImportField("status", "Status"),
        new AiImportField("shortdescription", "Short description"), new AiImportField("description", "Description"),
        new AiImportField("imageurl", "Image URL"), new AiImportField("attribute", "Keep as specification"),
        new AiImportField("ignore", "Ignore this column"),
    };
    private static readonly HashSet<string> ValidTargets = new(Fields.Select(f => f.Value), StringComparer.OrdinalIgnoreCase);
    private static readonly IReadOnlyList<AiImportField> Formats =
        MigrationPresets.All.Select(p => new AiImportField(p.Key, p.Label)).ToList();
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public async Task<AiImportAnalysis> AnalyzeAsync(Stream stream, string? fileName, string? format = null, CancellationToken ct = default)
    {
        var (headers, rows) = ReadTable(stream, fileName);
        if (headers.Count == 0) throw new AppException("No columns found in the file.", 400);

        // 1) Deterministic platform preset for recognised columns (free, accurate).
        var preset = MigrationPresets.Get(format) ?? MigrationPresets.Detect(headers);
        var mapping = new Dictionary<string, string>(StringComparer.Ordinal);
        var unmapped = new List<string>();
        foreach (var h in headers)
        {
            if (preset is not null && preset.Map.TryGetValue(h, out var t) && ValidTargets.Contains(t))
                mapping[h] = t.ToLowerInvariant();
            else
                unmapped.Add(h);
        }

        // 2) AI maps only what the preset didn't cover (metered only when the AI is actually called).
        if (unmapped.Count > 0 && ai.Enabled)
        {
            var aiMap = await credits.MeterAsync(AiCreditPricing.ColumnMap, async svc =>
            {
                var c = await svc.CompleteAsync(new AiPrompt(MapSystem, BuildMapUser(unmapped, headers, rows), Json: true, MaxTokens: 800), ct);
                return (ParseMapping(c.Text, unmapped), c);
            }, ct);
            foreach (var h in unmapped) mapping[h] = aiMap.TryGetValue(h, out var t) ? t : "ignore";
        }
        else
        {
            foreach (var h in unmapped) mapping[h] = "ignore";
        }

        var sample = rows.Take(5).Select(r => (IReadOnlyList<string>)Align(r, headers.Count)).ToList();
        return new AiImportAnalysis(headers, sample, mapping, Fields, preset?.Label, Formats);
    }

    public async Task<ImportResultDto> ApplyAsync(Stream stream, string? fileName, IReadOnlyDictionary<string, string> mapping, long? userId, CancellationToken ct = default)
    {
        var (headers, rows) = ReadTable(stream, fileName);

        // Resolve output columns: (outputHeader, sourceIndex). One column per canonical field (first wins).
        var outputs = new List<(string Header, int SourceIndex)>();
        var usedCanonical = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int? categoryIndex = null;
        for (var i = 0; i < headers.Count; i++)
        {
            var target = mapping.TryGetValue(headers[i], out var t) ? t : "ignore";
            if (string.Equals(target, "ignore", StringComparison.OrdinalIgnoreCase)) continue;
            if (string.Equals(target, "attribute", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(headers[i])) outputs.Add((headers[i], i));
            }
            else if (Canonical.TryGetValue(target, out var canon) && usedCanonical.Add(canon))
            {
                outputs.Add((canon, i));
                if (canon == "Category") categoryIndex = i;
            }
        }
        if (!outputs.Any(o => o.Header == "SKU") || !outputs.Any(o => o.Header == "Name"))
            throw new AppException("Map at least the SKU and Name columns before importing.", 400);

        // Auto-create categories the file references but the store doesn't have yet.
        if (categoryIndex is int ci)
            await EnsureCategoriesAsync(rows.Select(r => ci < r.Length ? r[ci].Trim() : "").Where(v => v.Length > 0), ct);

        var xlsx = BuildCanonicalXlsx(outputs, rows);
        using var ms = new MemoryStream(xlsx);
        return await importer.ImportAsync(ms, fileName, userId, ct);
    }

    private async Task EnsureCategoriesAsync(IEnumerable<string> names, CancellationToken ct)
    {
        var wanted = names.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (wanted.Count == 0) return;
        var existing = new HashSet<string>(await db.Categories.Select(c => c.Name).ToListAsync(ct), StringComparer.OrdinalIgnoreCase);
        var slugs = new HashSet<string>(await db.Categories.Select(c => c.Slug).ToListAsync(ct), StringComparer.OrdinalIgnoreCase);
        var now = DateTime.UtcNow;
        var added = false;
        foreach (var name in wanted.Where(n => !existing.Contains(n)))
        {
            var slug = Slug.From(name); var s = slug; var n = 2;
            while (!slugs.Add(s)) s = $"{slug}-{n++}";
            db.Categories.Add(new Category { Name = name.Length > 120 ? name[..120] : name, Slug = s, IsActive = true, CreatedAt = now });
            existing.Add(name);
            added = true;
        }
        if (added) await db.SaveChangesAsync(ct);
    }

    private static byte[] BuildCanonicalXlsx(IReadOnlyList<(string Header, int SourceIndex)> outputs, IReadOnlyList<string[]> rows)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Products");
        for (var c = 0; c < outputs.Count; c++) ws.Cell(1, c + 1).Value = outputs[c].Header;
        var r = 2;
        foreach (var row in rows)
        {
            for (var c = 0; c < outputs.Count; c++)
            {
                var idx = outputs[c].SourceIndex;
                var val = idx < row.Length ? StripHtml(row[idx]) : string.Empty;
                if (outputs[c].Header == "ImageUrl") val = FirstUrl(val);   // Woo/Shopify pack multiple images per cell
                ws.Cell(r, c + 1).Value = val;
            }
            r++;
        }
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    // ---- AI mapping ----
    private const string MapSystem =
        "You map a product spreadsheet's columns to a fixed e-commerce schema. " +
        "Valid targets: sku, name, category, brand, price, compareatprice, costprice, hsncode, status, shortdescription, description, imageurl, attribute, ignore. " +
        "Use \"attribute\" to keep a column as a product specification (e.g. Color, Size, Material). " +
        "Use \"ignore\" for internal IDs, handles/slugs, timestamps, inventory quantities/locations, SEO fields, variant plumbing. " +
        "Return STRICT JSON: {\"mapping\": {\"<sourceHeader>\": \"<target>\"}} with exactly one entry per source header. Output ONLY JSON.";

    private static string BuildMapUser(IReadOnlyList<string> subset, IReadOnlyList<string> allHeaders, IReadOnlyList<string[]> rows)
    {
        var sb = new StringBuilder("Source columns with sample values:\n");
        foreach (var h in subset)
        {
            var i = -1;
            for (var k = 0; k < allHeaders.Count; k++) if (allHeaders[k] == h) { i = k; break; }
            var samples = rows.Take(4).Select(r => i >= 0 && i < r.Length ? r[i].Trim() : "")
                .Where(v => v.Length > 0).Take(3).Select(v => "\"" + Trunc(StripHtml(v), 40).Replace("\"", "'") + "\"");
            sb.AppendLine($"- \"{h}\": [{string.Join(", ", samples)}]");
        }
        return sb.ToString();
    }

    private sealed record MapWrapper(Dictionary<string, string>? mapping);

    private static IReadOnlyDictionary<string, string> ParseMapping(string json, IReadOnlyList<string> headers)
    {
        Dictionary<string, string>? raw = null;
        try { raw = JsonSerializer.Deserialize<MapWrapper>(json, JsonOpts)?.mapping; }
        catch (JsonException) { /* fall back to all-ignore below */ }

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var h in headers)
        {
            var target = raw != null && raw.TryGetValue(h, out var t) && ValidTargets.Contains(t) ? t.ToLowerInvariant() : "ignore";
            result[h] = target;
        }
        return result;
    }

    // ---- file reading ----
    private static (List<string> Headers, List<string[]> Rows) ReadTable(Stream stream, string? fileName)
    {
        var isCsv = (fileName ?? "").EndsWith(".csv", StringComparison.OrdinalIgnoreCase);
        var all = isCsv ? ParseCsv(new StreamReader(stream).ReadToEnd()) : ReadXlsx(stream);
        if (all.Count == 0) throw new AppException("The file is empty.", 400);
        var headers = all[0].Select(h => (h ?? string.Empty).Trim()).ToList();
        var rows = all.Skip(1).Where(r => r.Any(c => !string.IsNullOrWhiteSpace(c))).ToList();
        return (headers, rows);
    }

    private static List<string[]> ReadXlsx(Stream stream)
    {
        using var wb = new XLWorkbook(stream);
        var ws = wb.Worksheet(1);
        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 0;
        var lastCol = ws.LastColumnUsed()?.ColumnNumber() ?? 0;
        var rows = new List<string[]>();
        for (var r = 1; r <= lastRow; r++)
        {
            var arr = new string[lastCol];
            for (var c = 1; c <= lastCol; c++) arr[c - 1] = ws.Cell(r, c).GetString().Trim();
            rows.Add(arr);
        }
        return rows;
    }

    private static List<string[]> ParseCsv(string text)
    {
        var rows = new List<string[]>();
        var field = new StringBuilder();
        var record = new List<string>();
        var inQuotes = false;
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                    else inQuotes = false;
                }
                else field.Append(ch);
            }
            else
            {
                switch (ch)
                {
                    case '"': inQuotes = true; break;
                    case ',': record.Add(field.ToString()); field.Clear(); break;
                    case '\r': break;
                    case '\n': record.Add(field.ToString()); field.Clear(); rows.Add(record.ToArray()); record = new List<string>(); break;
                    default: field.Append(ch); break;
                }
            }
        }
        if (field.Length > 0 || record.Count > 0) { record.Add(field.ToString()); rows.Add(record.ToArray()); }
        return rows;
    }

    // ---- helpers ----
    private static string[] Align(string[] row, int n)
    {
        var arr = new string[n];
        for (var i = 0; i < n; i++) arr[i] = i < row.Length ? row[i] : string.Empty;
        return arr;
    }

    private static string StripHtml(string s) =>
        string.IsNullOrEmpty(s) ? s : System.Text.RegularExpressions.Regex.Replace(s, "<.*?>", string.Empty).Trim();

    /// <summary>First URL from a cell that may pack several (comma/newline/semicolon separated).</summary>
    private static string FirstUrl(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return s;
        var i = s.IndexOfAny(new[] { ',', '\n', '\r', ';' });
        return (i < 0 ? s : s[..i]).Trim();
    }

    private static string Trunc(string s, int max) { s = (s ?? string.Empty).Trim(); return s.Length <= max ? s : s[..max]; }
}
