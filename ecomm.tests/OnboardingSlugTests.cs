using System.Text.RegularExpressions;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Auth.Dtos;
using ecomm.api.Features.Auth.Services;
using ecomm.api.Features.Cms;
using ecomm.api.Features.Onboarding;
using ecomm.api.Features.Storefront;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace ecomm.tests;

public class OnboardingSlugTests
{
    // Slug paths only use the DbContext; auth is never called → a throwing stub is enough.
    private sealed class StubAuth : IAuthService
    {
        public Task<AuthConfigResponse> GetConfigAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<AuthResponse> RegisterAsync(RegisterRequest r, string? ip, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<AuthResponse> LoginAsync(LoginRequest r, string? ip, CancellationToken ct = default) => throw new NotImplementedException();
        public Task RequestOtpAsync(OtpRequestDto r, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<AuthResponse> VerifyOtpAsync(OtpVerifyDto r, string? ip, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<AuthResponse> GoogleAsync(GoogleLoginRequest r, string? ip, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<AuthResponse> RefreshAsync(RefreshRequest r, string? ip, CancellationToken ct = default) => throw new NotImplementedException();
        public Task RequestPasswordResetAsync(string email, CancellationToken ct = default) => throw new NotImplementedException();
        public Task ResetPasswordAsync(ResetPasswordRequest r, CancellationToken ct = default) => throw new NotImplementedException();
        public Task RequestEmailVerificationAsync(long userId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<bool> ConfirmEmailVerificationAsync(long userId, string code, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<AuthResponse> IssueTokensForUserAsync(User user, string? ip, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private static OnboardingService NewSvc(ecomm.api.Data.Context.EcommerceDbContext db) =>
        new(db, new FixedTenant(1), new BcryptPasswordHasher(), new StubAuth(),
            new ThemeLibraryService(db, new CmsService(db)), NullLogger<OnboardingService>.Instance,
            Options.Create(new ecomm.api.Common.Tenancy.TenancyOptions { BaseDomain = "wavcommerce.online" }));

    [Fact]
    public async Task Short_slugs_are_rejected()
    {
        using var db = TestDb.New(tenantId: 1);
        var svc = NewSvc(db);

        Assert.False(await svc.IsSlugAvailableAsync("c", default));    // 1 char — the "c.wavcommerce.online" bug
        Assert.False(await svc.IsSlugAvailableAsync("ab", default));   // 2 chars
        Assert.True(await svc.IsSlugAvailableAsync("cafe24", default));
    }

    [Fact]
    public async Task Suggest_derives_a_unique_handle_from_the_name()
    {
        using var db = TestDb.New(tenantId: 1);
        var svc = NewSvc(db);

        var slug = await svc.SuggestSlugAsync("Cafe 24 !!", default);

        Assert.Matches(new Regex("^cafe-24-[a-z0-9]{4}$"), slug);   // name base + random suffix
        Assert.True(await svc.IsSlugAvailableAsync(slug, default));  // and it's actually free
    }

    [Fact]
    public async Task Suggest_falls_back_when_the_name_has_no_usable_letters()
    {
        using var db = TestDb.New(tenantId: 1);
        var svc = NewSvc(db);

        var slug = await svc.SuggestSlugAsync("!!!", default);
        Assert.Matches(new Regex("^store-[a-z0-9]{4}$"), slug);
    }

    [Fact]
    public async Task Suggest_avoids_a_taken_handle()
    {
        using var db = TestDb.New(tenantId: 1);
        var svc = NewSvc(db);
        db.Tenants.Add(new Tenant { Name = "X", Slug = "cafe24-aaaa", IsActive = true });
        await db.SaveChangesAsync();

        var slug = await svc.SuggestSlugAsync("Cafe24", default);
        Assert.NotEqual("cafe24-aaaa", slug);
        Assert.True(await svc.IsSlugAvailableAsync(slug, default));
    }
}
