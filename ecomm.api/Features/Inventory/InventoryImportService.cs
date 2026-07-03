using ClosedXML.Excel;
using ecomm.api.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Inventory;

public sealed record InventoryImportError(int RowNumber, string Message);
public sealed record InventoryImportResult(int Total, int Success, int Failed, List<InventoryImportError> Errors);

public interface IInventoryImportService
{
    Task<byte[]> ExportAsync(CancellationToken ct = default);
    byte[] Template();
    Task<InventoryImportResult> ImportAsync(Stream xlsx, long? userId, CancellationToken ct = default);
}

/// <summary>Excel import/export for stock levels (upsert by SKU). Reuses the inventory service.</summary>
public sealed class InventoryImportService : IInventoryImportService
{
    private long Tenant => _db.CurrentTenantId;
    private static readonly string[] Headers = ["SKU", "Name", "Available", "Reserved", "ReorderLevel"];

    private readonly EcommerceDbContext _db;
    private readonly IInventoryService _inventory;

    public InventoryImportService(EcommerceDbContext db, IInventoryService inventory)
    {
        _db = db;
        _inventory = inventory;
    }

    public byte[] Template()
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Inventory");
        string[] cols = ["SKU", "Available", "ReorderLevel"];
        for (var i = 0; i < cols.Length; i++) ws.Cell(1, i + 1).Value = cols[i];
        ws.Cell(2, 1).Value = "WALL-2026";
        ws.Cell(2, 2).Value = 100;
        ws.Cell(2, 3).Value = 20;
        ws.Row(1).Style.Font.Bold = true;
        return Save(wb);
    }

    public async Task<byte[]> ExportAsync(CancellationToken ct = default)
    {
        var rows = await _db.Products
            .Where(p => p.TenantId == Tenant && !p.IsDeleted)
            .OrderBy(p => p.Name)
            .Select(p => new
            {
                p.Sku, p.Name,
                Inv = _db.Inventory.FirstOrDefault(i => i.ProductId == p.ProductId && i.ProductVariantId == null),
            })
            .ToListAsync(ct);

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Inventory");
        for (var i = 0; i < Headers.Length; i++) ws.Cell(1, i + 1).Value = Headers[i];
        ws.Row(1).Style.Font.Bold = true;

        var r = 2;
        foreach (var x in rows)
        {
            ws.Cell(r, 1).Value = x.Sku;
            ws.Cell(r, 2).Value = x.Name;
            ws.Cell(r, 3).Value = x.Inv?.AvailableQty ?? 0;
            ws.Cell(r, 4).Value = x.Inv?.ReservedQty ?? 0;
            ws.Cell(r, 5).Value = x.Inv?.ReorderLevel ?? 0;
            r++;
        }
        return Save(wb);
    }

    public async Task<InventoryImportResult> ImportAsync(Stream xlsx, long? userId, CancellationToken ct = default)
    {
        using var wb = new XLWorkbook(xlsx);
        var ws = wb.Worksheet(1);
        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
        var cols = MapHeaders(ws);

        var errors = new List<InventoryImportError>();
        int total = 0, success = 0;

        for (var row = 2; row <= lastRow; row++)
        {
            if (ws.Row(row).IsEmpty()) continue;
            total++;
            string Cell(string name) => cols.TryGetValue(name, out var c) ? ws.Cell(row, c).GetString().Trim() : string.Empty;

            try
            {
                var sku = Cell("sku");
                if (string.IsNullOrWhiteSpace(sku)) throw new InvalidOperationException("SKU is required.");
                if (!int.TryParse(Cell("available"), out var available) || available < 0)
                    throw new InvalidOperationException($"Invalid Available: '{Cell("available")}'");
                int.TryParse(Cell("reorderlevel"), out var reorder);

                var productId = await _db.Products
                    .Where(p => p.TenantId == Tenant && p.Sku == sku && !p.IsDeleted)
                    .Select(p => (long?)p.ProductId).FirstOrDefaultAsync(ct);
                if (productId is null) throw new InvalidOperationException($"SKU not found: {sku}");

                await _inventory.SetStockAsync(productId.Value, new SetStockRequest(available, Math.Max(0, reorder)), userId, ct);
                success++;
            }
            catch (Exception ex)
            {
                errors.Add(new InventoryImportError(row, ex.Message));
            }
        }

        return new InventoryImportResult(total, success, total - success, errors);
    }

    private static Dictionary<string, int> MapHeaders(IXLWorksheet ws)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in ws.Row(1).CellsUsed())
        {
            var key = cell.GetString().Trim();
            if (key.Length > 0 && !map.ContainsKey(key)) map[key] = cell.Address.ColumnNumber;
        }
        return map;
    }

    private static byte[] Save(XLWorkbook wb)
    {
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}
