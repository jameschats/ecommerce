using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Account;

public interface IAccountService
{
    Task<ProfileDto?> GetProfileAsync(long userId, CancellationToken ct = default);
    Task<ProfileDto?> UpdateProfileAsync(long userId, UpdateProfileRequest req, CancellationToken ct = default);

    Task<List<AddressDto>> ListAddressesAsync(long userId, CancellationToken ct = default);
    Task<AddressDto> CreateAddressAsync(long userId, SaveAddressRequest req, CancellationToken ct = default);
    Task<AddressDto?> UpdateAddressAsync(long userId, long addressId, SaveAddressRequest req, CancellationToken ct = default);
    Task<bool> DeleteAddressAsync(long userId, long addressId, CancellationToken ct = default);
    Task<bool> SetDefaultAddressAsync(long userId, long addressId, CancellationToken ct = default);

    Task<List<NotificationPreferenceDto>> ListNotificationPreferencesAsync(long userId, CancellationToken ct = default);
    Task<NotificationPreferenceDto> SetNotificationPreferenceAsync(long userId, SetNotificationPreferenceRequest req, CancellationToken ct = default);
}

public sealed class AccountService : IAccountService
{
    private readonly EcommerceDbContext _db;
    public AccountService(EcommerceDbContext db) => _db = db;

    public async Task<ProfileDto?> GetProfileAsync(long userId, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId && !u.IsDeleted, ct);
        if (user is null) return null;
        var roles = await _db.UserRoles.Where(ur => ur.UserId == userId)
            .Join(_db.Roles, ur => ur.RoleId, r => r.RoleId, (ur, r) => r.Name).ToListAsync(ct);
        return new ProfileDto(user.UserId, user.Email, user.FullName, user.PhoneNumber, roles, user.IsEmailVerified);
    }

    public async Task<ProfileDto?> UpdateProfileAsync(long userId, UpdateProfileRequest req, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId && !u.IsDeleted, ct);
        if (user is null) return null;
        if (!string.IsNullOrWhiteSpace(req.FullName)) user.FullName = req.FullName.Trim();
        if (req.PhoneNumber is not null) user.PhoneNumber = string.IsNullOrWhiteSpace(req.PhoneNumber) ? null : req.PhoneNumber.Trim();
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return await GetProfileAsync(userId, ct);
    }

    public Task<List<AddressDto>> ListAddressesAsync(long userId, CancellationToken ct = default) =>
        _db.CustomerAddresses
            .Where(a => a.UserId == userId && !a.IsDeleted)
            .OrderByDescending(a => a.IsDefault).ThenByDescending(a => a.CustomerAddressId)
            .Select(a => new AddressDto(a.CustomerAddressId, a.Label, a.RecipientName, a.Phone, a.Line1, a.Line2, a.City, a.State, a.Pincode, a.Country, a.AddressType, a.IsDefault))
            .ToListAsync(ct);

    public async Task<AddressDto> CreateAddressAsync(long userId, SaveAddressRequest req, CancellationToken ct = default)
    {
        Validate(req);
        var hasAny = await _db.CustomerAddresses.AnyAsync(a => a.UserId == userId && !a.IsDeleted, ct);
        var entity = new CustomerAddress
        {
            UserId = userId,
            IsDefault = req.IsDefault || !hasAny,   // first address is default
            CreatedAt = DateTime.UtcNow,
        };
        Apply(entity, req);
        _db.CustomerAddresses.Add(entity);
        await _db.SaveChangesAsync(ct);
        if (entity.IsDefault) await ClearOtherDefaultsAsync(userId, entity.CustomerAddressId, ct);
        return ToDto(entity);
    }

    public async Task<AddressDto?> UpdateAddressAsync(long userId, long addressId, SaveAddressRequest req, CancellationToken ct = default)
    {
        var entity = await _db.CustomerAddresses.FirstOrDefaultAsync(a => a.CustomerAddressId == addressId && a.UserId == userId && !a.IsDeleted, ct);
        if (entity is null) return null;
        Validate(req);
        Apply(entity, req);
        entity.IsDefault = req.IsDefault || entity.IsDefault;
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        if (entity.IsDefault) await ClearOtherDefaultsAsync(userId, entity.CustomerAddressId, ct);
        return ToDto(entity);
    }

    public async Task<bool> DeleteAddressAsync(long userId, long addressId, CancellationToken ct = default)
    {
        var entity = await _db.CustomerAddresses.FirstOrDefaultAsync(a => a.CustomerAddressId == addressId && a.UserId == userId && !a.IsDeleted, ct);
        if (entity is null) return false;
        entity.IsDeleted = true;
        entity.IsDefault = false;
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        // promote another address to default if needed
        if (!await _db.CustomerAddresses.AnyAsync(a => a.UserId == userId && !a.IsDeleted && a.IsDefault, ct))
        {
            var next = await _db.CustomerAddresses.Where(a => a.UserId == userId && !a.IsDeleted)
                .OrderByDescending(a => a.CustomerAddressId).FirstOrDefaultAsync(ct);
            if (next is not null) { next.IsDefault = true; await _db.SaveChangesAsync(ct); }
        }
        return true;
    }

    public async Task<bool> SetDefaultAddressAsync(long userId, long addressId, CancellationToken ct = default)
    {
        var entity = await _db.CustomerAddresses.FirstOrDefaultAsync(a => a.CustomerAddressId == addressId && a.UserId == userId && !a.IsDeleted, ct);
        if (entity is null) return false;
        entity.IsDefault = true;
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await ClearOtherDefaultsAsync(userId, addressId, ct);
        return true;
    }

    public Task<List<NotificationPreferenceDto>> ListNotificationPreferencesAsync(long userId, CancellationToken ct = default) =>
        _db.UserNotificationPreferences.AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => new NotificationPreferenceDto(p.Channel, p.Category, p.IsOptedIn, p.OptedInAt))
            .ToListAsync(ct);

    public async Task<NotificationPreferenceDto> SetNotificationPreferenceAsync(long userId, SetNotificationPreferenceRequest req, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.Channel)) throw new AppException("Channel is required.");
        if (req.Category != "marketing" && req.Category != "transactional")
            throw new AppException("Category must be 'marketing' or 'transactional'.");

        var entity = await _db.UserNotificationPreferences.FirstOrDefaultAsync(
            p => p.UserId == userId && p.Channel == req.Channel && p.Category == req.Category, ct);
        if (entity is null)
        {
            entity = new UserNotificationPreference { UserId = userId, Channel = req.Channel, Category = req.Category };
            _db.UserNotificationPreferences.Add(entity);
        }
        entity.IsOptedIn = req.IsOptedIn;
        entity.OptedInAt = req.IsOptedIn ? DateTime.UtcNow : null;   // stamps the moment of THIS consent event; cleared on opt-out
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return new NotificationPreferenceDto(entity.Channel, entity.Category, entity.IsOptedIn, entity.OptedInAt);
    }

    private async Task ClearOtherDefaultsAsync(long userId, long keepId, CancellationToken ct)
    {
        var others = await _db.CustomerAddresses
            .Where(a => a.UserId == userId && !a.IsDeleted && a.IsDefault && a.CustomerAddressId != keepId)
            .ToListAsync(ct);
        if (others.Count == 0) return;
        foreach (var o in others) o.IsDefault = false;
        await _db.SaveChangesAsync(ct);
    }

    private static void Validate(SaveAddressRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Line1)) throw new AppException("Address line 1 is required.");
        if (string.IsNullOrWhiteSpace(req.City)) throw new AppException("City is required.");
        if (string.IsNullOrWhiteSpace(req.State)) throw new AppException("State is required.");
        if (string.IsNullOrWhiteSpace(req.Pincode)) throw new AppException("Pincode is required.");
    }

    private static void Apply(CustomerAddress e, SaveAddressRequest r)
    {
        e.Label = r.Label?.Trim();
        e.RecipientName = r.RecipientName?.Trim();
        e.Phone = r.Phone?.Trim();
        e.Line1 = r.Line1.Trim();
        e.Line2 = r.Line2?.Trim();
        e.City = r.City.Trim();
        e.State = r.State.Trim();
        e.Pincode = r.Pincode.Trim();
        e.Country = string.IsNullOrWhiteSpace(r.Country) ? "India" : r.Country.Trim();
        e.AddressType = string.IsNullOrWhiteSpace(r.AddressType) ? "Both" : r.AddressType.Trim();
    }

    private static AddressDto ToDto(CustomerAddress a) =>
        new(a.CustomerAddressId, a.Label, a.RecipientName, a.Phone, a.Line1, a.Line2, a.City, a.State, a.Pincode, a.Country, a.AddressType, a.IsDefault);
}
