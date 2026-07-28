using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.ColorSwatches;

public sealed record ColorSwatchDto(long ColorSwatchId, string Name, string HexCode);
public sealed record SaveColorSwatchRequest(string Name, string HexCode);

public interface IColorSwatchService
{
    Task<List<ColorSwatchDto>> ListAsync(CancellationToken ct = default);
    Task<ColorSwatchDto> CreateAsync(SaveColorSwatchRequest req, CancellationToken ct = default);
    Task<ColorSwatchDto> UpdateAsync(long id, SaveColorSwatchRequest req, CancellationToken ct = default);
    Task DeleteAsync(long id, CancellationToken ct = default);
}

public sealed class ColorSwatchService : IColorSwatchService
{
    private long Tenant => _db.CurrentTenantId;
    private readonly EcommerceDbContext _db;
    public ColorSwatchService(EcommerceDbContext db) => _db = db;

    public Task<List<ColorSwatchDto>> ListAsync(CancellationToken ct = default) =>
        _db.ColorSwatches.AsNoTracking().Where(c => c.TenantId == Tenant).OrderBy(c => c.Name)
            .Select(c => new ColorSwatchDto(c.ColorSwatchId, c.Name, c.HexCode))
            .ToListAsync(ct);

    public async Task<ColorSwatchDto> CreateAsync(SaveColorSwatchRequest req, CancellationToken ct = default)
    {
        Validate(req);
        if (await _db.ColorSwatches.AnyAsync(c => c.TenantId == Tenant && c.Name == req.Name.Trim(), ct))
            throw new AppException("A colour with this name already exists.");
        var c = new ColorSwatch { TenantId = Tenant, CreatedAt = DateTime.UtcNow };
        Apply(c, req);
        _db.ColorSwatches.Add(c);
        await _db.SaveChangesAsync(ct);
        return ToDto(c);
    }

    public async Task<ColorSwatchDto> UpdateAsync(long id, SaveColorSwatchRequest req, CancellationToken ct = default)
    {
        Validate(req);
        var c = await Find(id, ct);
        if (await _db.ColorSwatches.AnyAsync(x => x.TenantId == Tenant && x.Name == req.Name.Trim() && x.ColorSwatchId != id, ct))
            throw new AppException("A colour with this name already exists.");
        Apply(c, req);
        c.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ToDto(c);
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        var c = await Find(id, ct);
        _db.ColorSwatches.Remove(c);
        await _db.SaveChangesAsync(ct);
    }

    private async Task<ColorSwatch> Find(long id, CancellationToken ct) =>
        await _db.ColorSwatches.FirstOrDefaultAsync(c => c.TenantId == Tenant && c.ColorSwatchId == id, ct)
        ?? throw new AppException("Colour swatch not found.", 404);

    private static void Validate(SaveColorSwatchRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Name)) throw new AppException("Colour name is required.");
        if (string.IsNullOrWhiteSpace(r.HexCode) || !System.Text.RegularExpressions.Regex.IsMatch(r.HexCode, "^#[0-9A-Fa-f]{6}$"))
            throw new AppException("Hex code must look like #RRGGBB.");
    }

    private static void Apply(ColorSwatch c, SaveColorSwatchRequest r)
    {
        c.Name = r.Name.Trim();
        c.HexCode = r.HexCode.Trim().ToUpperInvariant();
    }

    private static ColorSwatchDto ToDto(ColorSwatch c) => new(c.ColorSwatchId, c.Name, c.HexCode);
}
