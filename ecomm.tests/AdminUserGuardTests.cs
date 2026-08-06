using System.Security.Claims;
using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Context;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Auth.Dtos;
using ecomm.api.Features.Auth.Services;
using ecomm.api.Features.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ecomm.tests;

/// <summary>
/// The guards on the users screen — the rules that stop it being used to lock the shop out of
/// its own back office, or to strand an account nobody can reach.
///
/// These are cheap to get wrong and expensive to discover: every one of them only matters on
/// the day somebody does the thing it prevents.
/// </summary>
public class AdminUserGuardTests
{
    private const long Tenant = 1;

    /// <summary>SendReset is the only method that touches it, and none of these tests call it.</summary>
    private sealed class UnusedAuthService : IAuthService
    {
        private static Task Never => Task.FromException(new InvalidOperationException("not used in these tests"));
        public Task<AuthConfigResponse> GetConfigAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<AuthResponse> RegisterAsync(RegisterRequest r, string? ip, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<AuthResponse> LoginAsync(LoginRequest r, string? ip, CancellationToken ct = default) => throw new NotSupportedException();
        public Task RequestOtpAsync(OtpRequestDto r, CancellationToken ct = default) => Never;
        public Task<AuthResponse> VerifyOtpAsync(OtpVerifyDto r, string? ip, CancellationToken ct = default) => throw new NotSupportedException();
        public Task RequestEmailOtpAsync(string email, CancellationToken ct = default) => Never;
        public Task<AuthResponse> VerifyEmailOtpAsync(string e, string c, string? ip, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<AuthResponse> GoogleAsync(GoogleLoginRequest r, string? ip, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<AuthResponse> RefreshAsync(RefreshRequest r, string? ip, CancellationToken ct = default) => throw new NotSupportedException();
        public Task RequestPasswordResetAsync(string email, CancellationToken ct = default) => Never;
        public Task ResetPasswordAsync(ResetPasswordRequest r, CancellationToken ct = default) => Never;
        public Task ChangePasswordAsync(long userId, ChangePasswordRequest r, CancellationToken ct = default) => Never;
        public Task RequestEmailVerificationAsync(long userId, CancellationToken ct = default) => Never;
        public Task<bool> ConfirmEmailVerificationAsync(long userId, string code, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private static UsersAdminController Controller(EcommerceDbContext db, long signedInAs)
    {
        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, signedInAs.ToString()) }, "test");
        return new UsersAdminController(db, new BcryptPasswordHasher(), new UnusedAuthService())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) },
            },
        };
    }

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

    private static void AddUser(EcommerceDbContext db, long id, string email, long? roleId, bool active = true)
    {
        db.Users.Add(new User
        {
            UserId = id, TenantId = Tenant, Email = email, NormalizedEmail = email.ToUpperInvariant(),
            IsActive = active, CreatedAt = DateTime.UtcNow, PasswordHash = "x",
        });
        if (roleId is not null) db.UserRoles.Add(new UserRole { UserId = id, RoleId = roleId.Value });
        db.SaveChanges();
    }

    [Fact]
    public async Task The_only_administrator_cannot_be_deleted()
    {
        // Someone has to be able to get back in tomorrow.
        using var db = Seed();
        AddUser(db, 1, "admin@shop.test", roleId: 1);
        AddUser(db, 2, "boss@shop.test", roleId: 1);          // a second admin, doing the deleting
        db.UserRoles.Remove(db.UserRoles.First(ur => ur.UserId == 2));
        db.SaveChanges();
        db.UserRoles.Add(new UserRole { UserId = 2, RoleId = 3 });  // ...is actually only Staff
        db.SaveChanges();

        var ex = await Assert.ThrowsAsync<AppException>(() => Controller(db, signedInAs: 2).Delete(1, default));
        Assert.Contains("only administrator", ex.Message);
    }

    [Fact]
    public async Task A_second_administrator_makes_the_first_removable()
    {
        using var db = Seed();
        AddUser(db, 1, "admin@shop.test", roleId: 1);
        AddUser(db, 2, "second@shop.test", roleId: 1);

        await Controller(db, signedInAs: 2).Delete(1, default);
        Assert.True(db.Users.Single(u => u.UserId == 1).IsDeleted);
    }

    [Fact]
    public async Task An_inactive_administrator_does_not_count_as_a_way_back_in()
    {
        // A switched-off admin cannot sign in, so leaning on them as "the other administrator"
        // would lock everyone out while looking safe.
        using var db = Seed();
        AddUser(db, 1, "admin@shop.test", roleId: 1);
        AddUser(db, 2, "dormant@shop.test", roleId: 1, active: false);
        AddUser(db, 3, "staff@shop.test", roleId: 3);

        var ex = await Assert.ThrowsAsync<AppException>(() => Controller(db, signedInAs: 3).Delete(1, default));
        Assert.Contains("only administrator", ex.Message);
    }

    [Fact]
    public async Task You_cannot_switch_yourself_off()
    {
        using var db = Seed();
        AddUser(db, 1, "admin@shop.test", roleId: 1);
        AddUser(db, 2, "second@shop.test", roleId: 1);

        var ex = await Assert.ThrowsAsync<AppException>(
            () => Controller(db, signedInAs: 1).SetActive(1, new SetActiveRequest(false), default));
        Assert.Contains("your own account", ex.Message);
    }

    [Fact]
    public async Task Customers_are_not_editable_from_the_staff_screen()
    {
        using var db = Seed();
        AddUser(db, 1, "admin@shop.test", roleId: 1);
        AddUser(db, 2, "shopper@shop.test", roleId: 2);

        var ex = await Assert.ThrowsAsync<AppException>(() => Controller(db, signedInAs: 1).Delete(2, default));
        Assert.Contains("customer account", ex.Message);
    }

    [Fact]
    public async Task An_account_stripped_of_every_role_is_still_manageable()
    {
        // Otherwise clearing someone's roles strands them: editable by nobody, deletable by
        // nobody, and still sitting in the list.
        using var db = Seed();
        AddUser(db, 1, "admin@shop.test", roleId: 1);
        AddUser(db, 2, "stripped@shop.test", roleId: null);

        await Controller(db, signedInAs: 1).Delete(2, default);
        Assert.True(db.Users.Single(u => u.UserId == 2).IsDeleted);
    }

    [Fact]
    public async Task Deleting_releases_the_email_and_phone_for_reuse()
    {
        using var db = Seed();
        AddUser(db, 1, "admin@shop.test", roleId: 1);
        AddUser(db, 2, "leaver@shop.test", roleId: 3);
        db.Users.Single(u => u.UserId == 2).PhoneNumber = "9876500111";
        db.SaveChanges();

        await Controller(db, signedInAs: 1).Delete(2, default);

        var gone = db.Users.Single(u => u.UserId == 2);
        Assert.True(gone.IsDeleted);
        Assert.False(gone.IsActive);
        Assert.Null(gone.PhoneNumber);
        Assert.DoesNotContain("leaver@shop.test", gone.Email);      // renamed out of the way
        Assert.Empty(db.UserRoles.Where(ur => ur.UserId == 2));

        // ...and the address is genuinely free again.
        var created = await Controller(db, signedInAs: 1).Create(
            new CreateStaffRequest("Replacement", "leaver@shop.test", "9876500111", "Test@1234", ["Staff"]), default);
        Assert.IsType<OkObjectResult>(created);
    }

    [Fact]
    public async Task Switching_someone_off_ends_their_live_sessions()
    {
        using var db = Seed();
        AddUser(db, 1, "admin@shop.test", roleId: 1);
        AddUser(db, 2, "staff@shop.test", roleId: 3);
        db.RefreshTokens.Add(new RefreshToken
        {
            RefreshTokenId = 1, UserId = 2, TokenHash = "live",
            ExpiresAt = DateTime.UtcNow.AddDays(7), CreatedAt = DateTime.UtcNow,
        });
        db.SaveChanges();

        await Controller(db, signedInAs: 1).SetActive(2, new SetActiveRequest(false), default);

        Assert.NotNull(db.RefreshTokens.Single().RevokedAt);
    }
}
