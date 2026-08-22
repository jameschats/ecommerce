using ecomm.api.Common.Exceptions;
using ecomm.api.Features.PublicApi;
using Xunit;

namespace ecomm.tests;

public class ApiKeyServiceTests
{
    [Fact]
    public async Task Creating_a_key_returns_the_raw_value_exactly_once()
    {
        using var db = TestDb.New(tenantId: 1);
        var svc = new ApiKeyService(db);

        var created = await svc.CreateAsync(new CreateApiKeyRequest("My integration", ["products:read"]), userId: 5);

        Assert.StartsWith(ApiKeyService.KeyPrefixLiteral, created.RawKey);
        Assert.Equal(["products:read"], created.Scopes);

        var listed = await svc.ListAsync();
        Assert.Single(listed);
        // The list view only ever exposes the prefix, never enough to reconstruct the raw key.
        Assert.True(listed[0].KeyPrefix.Length < created.RawKey.Length);
        Assert.StartsWith(listed[0].KeyPrefix, created.RawKey);
    }

    [Fact]
    public async Task Two_created_keys_never_collide()
    {
        using var db = TestDb.New(tenantId: 1);
        var svc = new ApiKeyService(db);

        var a = await svc.CreateAsync(new CreateApiKeyRequest("A", ["products:read"]), 5);
        var b = await svc.CreateAsync(new CreateApiKeyRequest("B", ["products:read"]), 5);

        Assert.NotEqual(a.RawKey, b.RawKey);
        Assert.NotEqual(ApiKeyService.Hash(a.RawKey), ApiKeyService.Hash(b.RawKey));
    }

    [Fact]
    public async Task An_unknown_scope_is_rejected()
    {
        using var db = TestDb.New(tenantId: 1);
        var svc = new ApiKeyService(db);

        await Assert.ThrowsAsync<AppException>(() => svc.CreateAsync(new CreateApiKeyRequest("X", ["products:delete-everything"]), 5));
    }

    [Fact]
    public async Task At_least_one_scope_is_required()
    {
        using var db = TestDb.New(tenantId: 1);
        var svc = new ApiKeyService(db);

        await Assert.ThrowsAsync<AppException>(() => svc.CreateAsync(new CreateApiKeyRequest("X", []), 5));
    }

    [Fact]
    public async Task A_label_is_required()
    {
        using var db = TestDb.New(tenantId: 1);
        var svc = new ApiKeyService(db);

        await Assert.ThrowsAsync<AppException>(() => svc.CreateAsync(new CreateApiKeyRequest("  ", ["products:read"]), 5));
    }

    [Fact]
    public async Task Revoking_a_key_stamps_RevokedAt_and_it_stays_listed()
    {
        using var db = TestDb.New(tenantId: 1);
        var svc = new ApiKeyService(db);
        var created = await svc.CreateAsync(new CreateApiKeyRequest("X", ["products:read"]), 5);

        await svc.RevokeAsync(created.Id);

        var listed = (await svc.ListAsync()).Single();
        Assert.NotNull(listed.RevokedAt);   // still visible in the list (audit trail), just marked revoked
    }

    [Fact]
    public async Task Revoking_an_unknown_key_throws()
    {
        using var db = TestDb.New(tenantId: 1);
        var svc = new ApiKeyService(db);

        await Assert.ThrowsAsync<AppException>(() => svc.RevokeAsync(999));
    }

    [Fact]
    public void Hash_is_deterministic_and_case_of_input_matters()
    {
        var h1 = ApiKeyService.Hash("wck_live_abc123");
        var h2 = ApiKeyService.Hash("wck_live_abc123");
        var h3 = ApiKeyService.Hash("wck_live_ABC123");

        Assert.Equal(h1, h2);
        Assert.NotEqual(h1, h3);
    }

    [Fact]
    public async Task Duplicate_scopes_in_the_request_are_deduplicated()
    {
        using var db = TestDb.New(tenantId: 1);
        var svc = new ApiKeyService(db);

        var created = await svc.CreateAsync(new CreateApiKeyRequest("X", ["products:read", "products:read", "PRODUCTS:READ"]), 5);

        Assert.Single(created.Scopes);
    }
}
