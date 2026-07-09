using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Shipping;

public sealed record ShippingMethodDto(
    long ShippingMethodId, string Name, string? Description, decimal BaseRate,
    decimal? FreeShippingThreshold, int? EstimatedDays, bool IsActive);

public sealed record SaveShippingMethodRequest(
    string Name, string? Description, decimal BaseRate, decimal? FreeShippingThreshold, int? EstimatedDays, bool IsActive);

public sealed record ShippingZoneDto(
    long ShippingZoneId, string Name, string? PincodeStart, string? PincodeEnd, decimal Rate, bool IsServiceable);

public sealed record SaveShippingZoneRequest(
    string Name, string? PincodeStart, string? PincodeEnd, decimal Rate, bool IsServiceable);

public interface IShippingAdminService
{
    Task<IReadOnlyList<ShippingMethodDto>> ListMethodsAsync(CancellationToken ct = default);
    Task<ShippingMethodDto> CreateMethodAsync(SaveShippingMethodRequest req, CancellationToken ct = default);
    Task<ShippingMethodDto> UpdateMethodAsync(long id, SaveShippingMethodRequest req, CancellationToken ct = default);
    Task DeleteMethodAsync(long id, CancellationToken ct = default);

    Task<IReadOnlyList<ShippingZoneDto>> ListZonesAsync(CancellationToken ct = default);
    Task<ShippingZoneDto> CreateZoneAsync(SaveShippingZoneRequest req, CancellationToken ct = default);
    Task<ShippingZoneDto> UpdateZoneAsync(long id, SaveShippingZoneRequest req, CancellationToken ct = default);
    Task DeleteZoneAsync(long id, CancellationToken ct = default);
}

/// <summary>
/// Merchant-admin CRUD for the shipping engine (<see cref="ShippingMethod"/> + <see cref="ShippingZone"/>)
/// that <c>Features/Checkout/ShippingService</c> already reads at checkout: the first active method sets
/// the base rate / free-shipping threshold / ETA; zones set per-pincode-range serviceability + rate.
/// Tenant-scoped by the global query filters.
/// </summary>
public sealed class ShippingAdminService(EcommerceDbContext db) : IShippingAdminService
{
    // ---- methods ----
    public async Task<IReadOnlyList<ShippingMethodDto>> ListMethodsAsync(CancellationToken ct = default) =>
        await db.ShippingMethods.OrderBy(m => m.ShippingMethodId)
            .Select(m => new ShippingMethodDto(m.ShippingMethodId, m.Name, m.Description, m.BaseRate, m.FreeShippingThreshold, m.EstimatedDays, m.IsActive))
            .ToListAsync(ct);

    public async Task<ShippingMethodDto> CreateMethodAsync(SaveShippingMethodRequest req, CancellationToken ct = default)
    {
        Validate(req);
        var m = new ShippingMethod
        {
            Name = req.Name.Trim(), Description = req.Description?.Trim(), RateType = "Flat",
            BaseRate = req.BaseRate, FreeShippingThreshold = req.FreeShippingThreshold,
            EstimatedDays = req.EstimatedDays, IsActive = req.IsActive, CreatedAt = DateTime.UtcNow,
        };
        db.ShippingMethods.Add(m);
        await db.SaveChangesAsync(ct);
        return ToDto(m);
    }

    public async Task<ShippingMethodDto> UpdateMethodAsync(long id, SaveShippingMethodRequest req, CancellationToken ct = default)
    {
        Validate(req);
        var m = await db.ShippingMethods.FirstOrDefaultAsync(x => x.ShippingMethodId == id, ct) ?? throw NotFound("Shipping method");
        m.Name = req.Name.Trim();
        m.Description = req.Description?.Trim();
        m.BaseRate = req.BaseRate;
        m.FreeShippingThreshold = req.FreeShippingThreshold;
        m.EstimatedDays = req.EstimatedDays;
        m.IsActive = req.IsActive;
        m.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return ToDto(m);
    }

    public async Task DeleteMethodAsync(long id, CancellationToken ct = default)
    {
        var m = await db.ShippingMethods.FirstOrDefaultAsync(x => x.ShippingMethodId == id, ct) ?? throw NotFound("Shipping method");
        db.ShippingMethods.Remove(m);
        await db.SaveChangesAsync(ct);
    }

    // ---- zones ----
    public async Task<IReadOnlyList<ShippingZoneDto>> ListZonesAsync(CancellationToken ct = default) =>
        await db.ShippingZones.OrderBy(z => z.ShippingZoneId)
            .Select(z => new ShippingZoneDto(z.ShippingZoneId, z.Name, z.PincodeStart, z.PincodeEnd, z.Rate, z.IsServiceable))
            .ToListAsync(ct);

    public async Task<ShippingZoneDto> CreateZoneAsync(SaveShippingZoneRequest req, CancellationToken ct = default)
    {
        ValidateZone(req);
        var z = new ShippingZone
        {
            Name = req.Name.Trim(), PincodeStart = req.PincodeStart?.Trim(), PincodeEnd = req.PincodeEnd?.Trim(),
            Rate = req.Rate, IsServiceable = req.IsServiceable, CreatedAt = DateTime.UtcNow,
        };
        db.ShippingZones.Add(z);
        await db.SaveChangesAsync(ct);
        return ToDto(z);
    }

    public async Task<ShippingZoneDto> UpdateZoneAsync(long id, SaveShippingZoneRequest req, CancellationToken ct = default)
    {
        ValidateZone(req);
        var z = await db.ShippingZones.FirstOrDefaultAsync(x => x.ShippingZoneId == id, ct) ?? throw NotFound("Shipping zone");
        z.Name = req.Name.Trim();
        z.PincodeStart = req.PincodeStart?.Trim();
        z.PincodeEnd = req.PincodeEnd?.Trim();
        z.Rate = req.Rate;
        z.IsServiceable = req.IsServiceable;
        z.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return ToDto(z);
    }

    public async Task DeleteZoneAsync(long id, CancellationToken ct = default)
    {
        var z = await db.ShippingZones.FirstOrDefaultAsync(x => x.ShippingZoneId == id, ct) ?? throw NotFound("Shipping zone");
        db.ShippingZones.Remove(z);
        await db.SaveChangesAsync(ct);
    }

    // ---- helpers ----
    private static void Validate(SaveShippingMethodRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name)) throw new AppException("Name is required.");
        if (req.BaseRate < 0) throw new AppException("Rate can't be negative.");
        if (req.FreeShippingThreshold is < 0) throw new AppException("Free-shipping threshold can't be negative.");
    }

    private static void ValidateZone(SaveShippingZoneRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name)) throw new AppException("Name is required.");
        if (req.Rate < 0) throw new AppException("Rate can't be negative.");
        var s = req.PincodeStart?.Trim();
        var e = req.PincodeEnd?.Trim();
        if (!string.IsNullOrEmpty(s) ^ !string.IsNullOrEmpty(e))
            throw new AppException("Give both a start and end pincode, or leave both blank.");
        if (!string.IsNullOrEmpty(s) && !string.IsNullOrEmpty(e) && string.CompareOrdinal(s, e) > 0)
            throw new AppException("Start pincode must be ≤ end pincode.");
    }

    private static ShippingMethodDto ToDto(ShippingMethod m) =>
        new(m.ShippingMethodId, m.Name, m.Description, m.BaseRate, m.FreeShippingThreshold, m.EstimatedDays, m.IsActive);
    private static ShippingZoneDto ToDto(ShippingZone z) =>
        new(z.ShippingZoneId, z.Name, z.PincodeStart, z.PincodeEnd, z.Rate, z.IsServiceable);
    private static AppException NotFound(string what) => new($"{what} not found.", StatusCodes.Status404NotFound);
}
