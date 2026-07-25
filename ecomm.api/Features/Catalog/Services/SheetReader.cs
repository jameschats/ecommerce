using System.Text;
using ClosedXML.Excel;
using ecomm.api.Common.Exceptions;

namespace ecomm.api.Features.Catalog.Services;

/// <summary>A parsed spreadsheet: the header row, and each data row as a dictionary.</summary>
public sealed record SheetData(IReadOnlyList<string> Headers, IReadOnlyList<SheetRow> Rows);

/// <param name="RowNumber">1-based row number as it appears in the file, so errors can name it.</param>
public sealed record SheetRow(int RowNumber, IReadOnlyDictionary<string, string> Values)
{
    public string Get(string header) =>
        Values.TryGetValue(header, out var v) ? v.Trim() : string.Empty;
}

/// <summary>
/// Reads .xlsx and .csv into one shape, so the importer has a single code path
/// (design.md §23).
///
/// The CSV branch is deliberately strict about encoding — see <see cref="ReadCsv"/>.
/// </summary>
public static class SheetReader
{
    public static SheetData Read(Stream stream, string? fileName)
    {
        var isCsv = fileName?.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) == true;
        return isCsv ? ReadCsv(stream) : ReadXlsx(stream);
    }

    private static SheetData ReadXlsx(Stream stream)
    {
        using var wb = new XLWorkbook(stream);
        var ws = wb.Worksheet(1);
        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;

        var headers = new List<string>();
        var columnOf = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in ws.Row(1).CellsUsed())
        {
            var key = cell.GetString().Trim();
            if (key.Length == 0 || columnOf.ContainsKey(key)) continue;
            columnOf[key] = cell.Address.ColumnNumber;
            headers.Add(key);
        }

        var rows = new List<SheetRow>();
        for (var r = 2; r <= lastRow; r++)
        {
            if (ws.Row(r).IsEmpty()) continue;
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (header, col) in columnOf) values[header] = ws.Cell(r, col).GetString().Trim();
            rows.Add(new SheetRow(r, values));
        }

        return new SheetData(headers, rows);
    }

    /// <summary>
    /// Reads a CSV, rejecting anything that is not valid UTF-8.
    ///
    /// This matters more than it looks. Excel's default "Save as CSV" writes the system
    /// ANSI codepage, which destroys Tamil — திருக்குறள் arrives as ???????? — and it does
    /// so silently. Importing mojibake is far worse than refusing the file, because the
    /// damage is only discovered later, in the catalogue, with no way to tell what the
    /// original said. So: decode strictly, and tell the user exactly which option to pick.
    /// </summary>
    private static SheetData ReadCsv(Stream stream)
    {
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        var bytes = buffer.ToArray();

        string text;
        try
        {
            text = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
                .GetString(StripBom(bytes));
        }
        catch (DecoderFallbackException)
        {
            throw new AppException(
                "That CSV is not saved as UTF-8, so any Tamil or special characters would be corrupted. "
                + "In Excel choose File → Save As → \"CSV UTF-8 (Comma delimited)\", or upload the .xlsx instead.");
        }

        var lines = SplitRecords(text);
        if (lines.Count == 0) throw new AppException("That CSV appears to be empty.");

        var headers = ParseLine(lines[0]).Select(h => h.Trim()).Where(h => h.Length > 0).ToList();
        if (headers.Count == 0) throw new AppException("The first row must contain column headings.");

        var rows = new List<SheetRow>();
        for (var i = 1; i < lines.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            var cells = ParseLine(lines[i]);
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var c = 0; c < headers.Count; c++)
                values[headers[c]] = c < cells.Count ? cells[c].Trim() : string.Empty;

            // +1 so the number matches what the user sees in a spreadsheet app.
            rows.Add(new SheetRow(i + 1, values));
        }

        return new SheetData(headers, rows);
    }

    private static byte[] StripBom(byte[] bytes) =>
        bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF
            ? bytes[3..]
            : bytes;

    /// <summary>
    /// Splits into records, respecting quoted fields. A newline inside quotes is part of the
    /// value, not a row break — descriptions routinely contain them.
    /// </summary>
    private static List<string> SplitRecords(string text)
    {
        var records = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];

            if (ch == '"')
            {
                // A doubled quote inside a quoted field is an escaped quote, not a delimiter.
                if (inQuotes && i + 1 < text.Length && text[i + 1] == '"') { current.Append("\"\""); i++; continue; }
                inQuotes = !inQuotes;
                current.Append(ch);
                continue;
            }

            if (!inQuotes && (ch == '\n' || ch == '\r'))
            {
                if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                records.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(ch);
        }

        if (current.Length > 0) records.Add(current.ToString());
        return records;
    }

    private static List<string> ParseLine(string line)
    {
        var cells = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];

            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"') { current.Append('"'); i++; continue; }
                inQuotes = !inQuotes;
                continue;
            }

            if (ch == ',' && !inQuotes) { cells.Add(current.ToString()); current.Clear(); continue; }
            current.Append(ch);
        }

        cells.Add(current.ToString());
        return cells;
    }
}
