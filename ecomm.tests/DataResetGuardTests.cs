using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Settings;
using Microsoft.Extensions.Logging.Abstractions;

namespace ecomm.tests;

/// <summary>
/// The guards on the go-live reset — the screen that deletes every order, invoice and payment
/// in one press.
///
/// Only the checks that run before anything is touched are exercised here: the deletion itself
/// is raw SQL against MySQL's cascades and cannot run on the in-memory provider. That is the
/// right split anyway. The deletes are the easy half; these are the half that decides whether
/// the deletes happen at all, and each one only matters on the day somebody does the thing it
/// prevents.
/// </summary>
public class DataResetGuardTests
{
    private const long Tenant = 1;

    private static DataResetService Service(EcommerceDbContext db) =>
        new(db, NullLogger<DataResetService>.Instance);

    private static EcommerceDbContext Seed()
    {
        var db = TestDb.New();
        db.Roles.AddRange(
            new Role { RoleId = 1, TenantId = Tenant, Name = "Admin", NormalizedName = "ADMIN", IsSystem = true },
            new Role { RoleId = 2, TenantId = Tenant, Name = "Customer", NormalizedName = "CUSTOMER", IsSystem = true },
            new Role { RoleId = 3, TenantId = Tenant, Name = "Staff", NormalizedName = "STAFF" });
        db.SaveChanges();
        return db;
    }

    private static void AddUser(EcommerceDbContext db, long id, string email, long? roleId)
    {
        db.Users.Add(new User
        {
            UserId = id, TenantId = Tenant, Email = email, NormalizedEmail = email.ToUpperInvariant(),
            IsActive = true, CreatedAt = DateTime.UtcNow, PasswordHash = "x",
        });
        if (roleId is not null) db.UserRoles.Add(new UserRole { UserId = id, RoleId = roleId.Value });
        db.SaveChanges();
    }

    private static void MarkLive(EcommerceDbContext db, DateTime when)
    {
        db.Settings.Add(new Setting
        {
            TenantId = Tenant,
            SettingKey = DataResetService.LiveSinceKey,
            SettingValue = when.ToString("O"),
            DataType = "string",
            Category = "Store",
            CreatedAt = DateTime.UtcNow,
        });
        db.SaveChanges();
    }

    [Fact]
    public async Task A_live_shop_refuses_the_reset()
    {
        // The whole point of the feature. Once real money has moved, no admin screen gets to
        // wipe the record of it.
        using var db = Seed();
        MarkLive(db, new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc));

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            Service(db).ResetAsync(DataResetService.ConfirmPhrase, new ResetOptions(), 1));

        Assert.Equal(409, ex.StatusCode);
        Assert.Contains("1 March 2026", ex.Message);
    }

    [Fact]
    public async Task The_live_check_runs_before_the_confirmation_phrase()
    {
        // A live shop is refused whatever was typed. If the order were reversed, a wrong phrase
        // on a live shop would report a typo and imply the right one would have worked.
        using var db = Seed();
        MarkLive(db, DateTime.UtcNow);

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            Service(db).ResetAsync("whatever", new ResetOptions(), 1));

        Assert.Equal(409, ex.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("delete all transactions")]   // right words, wrong case
    [InlineData("DELETE ALL TRANSACTION")]    // one letter short
    [InlineData("yes")]
    public async Task Anything_but_the_exact_phrase_is_refused(string typed)
    {
        using var db = Seed();

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            Service(db).ResetAsync(typed, new ResetOptions(), 1));

        Assert.Contains(DataResetService.ConfirmPhrase, ex.Message);
    }

    [Fact]
    public async Task A_null_phrase_is_refused_rather_than_crashing()
    {
        using var db = Seed();

        await Assert.ThrowsAsync<AppException>(() =>
            Service(db).ResetAsync(null!, new ResetOptions(), 1));
    }

    [Fact]
    public async Task Surrounding_whitespace_is_forgiven()
    {
        // Pasted from these notes, it arrives with a trailing space. Refusing that would teach
        // people to fight the box rather than read it. It gets past the phrase check and then
        // fails on the SQL the in-memory provider cannot run — which is the proof it got through.
        using var db = Seed();

        var ex = await Record.ExceptionAsync(() =>
            Service(db).ResetAsync($"  {DataResetService.ConfirmPhrase}  ", new ResetOptions(), 1));

        Assert.False(ex is AppException, $"Padded phrase was rejected: {(ex as AppException)?.Message}");
    }

    [Fact]
    public async Task Marking_live_is_recorded_once_and_never_moves()
    {
        // Pressing it twice must not quietly restamp the date — the date is the evidence of when
        // trading began.
        using var db = Seed();

        var first = await Service(db).GoLiveAsync();
        await Task.Delay(10);
        var second = await Service(db).GoLiveAsync();

        Assert.Equal(first, second);
        Assert.Single(db.Settings.Where(s => s.SettingKey == DataResetService.LiveSinceKey));
    }

    [Fact]
    public async Task Marking_live_closes_the_door_immediately()
    {
        using var db = Seed();
        var service = Service(db);

        await service.GoLiveAsync();

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            service.ResetAsync(DataResetService.ConfirmPhrase, new ResetOptions(), 1));
        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task Staff_and_the_signed_in_admin_are_never_counted_as_customers()
    {
        // Deleting customer accounts must not take the back office with it. Reached through the
        // public surface: a customer with a staff role alongside is staff, and the acting admin
        // is excluded even when they hold nothing but Customer.
        using var db = Seed();
        AddUser(db, 1, "admin@shop.test", roleId: 1);
        AddUser(db, 2, "packer@shop.test", roleId: 3);
        AddUser(db, 3, "buyer@shop.test", roleId: 2);
        AddUser(db, 4, "walkin@shop.test", roleId: null);     // no role at all — still a customer

        // Straight at the rule. PreviewAsync would be the nicer entry point, but its counts are
        // raw SQL and the in-memory provider cannot run them.
        var goners = await Service(db).CustomerIdsAsync(exclude: 3);

        Assert.Equal(new long[] { 4 }, goners);   // not the admin, not the packer, not the caller
    }
}
