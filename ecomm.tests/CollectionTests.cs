using ecomm.api.Data.Entities;
using ecomm.api.Features.Collections;
using Xunit;

namespace ecomm.tests;

public class CollectionTests
{
    private static async Task<(ecomm.api.Data.Context.EcommerceDbContext db, CollectionService svc)> SetupAsync()
    {
        var db = TestDb.New(tenantId: 1);
        var now = DateTime.UtcNow;
        db.Products.Add(new Product { ProductId = 1, Name = "Red Shirt", Slug = "red", Sku = "R", Price = 150m, Tags = "sale,summer", ProductType = "Shirt", Status = "Active", IsActive = true, CategoryId = 1, CreatedAt = now });
        db.Products.Add(new Product { ProductId = 2, Name = "Blue Mug", Slug = "blue", Sku = "B", Price = 80m, Tags = "clearance", ProductType = "Mug", Status = "Active", IsActive = true, CategoryId = 1, CreatedAt = now });
        db.Products.Add(new Product { ProductId = 3, Name = "Hidden", Slug = "hid", Sku = "H", Price = 200m, Tags = "sale", Status = "Draft", IsActive = true, CategoryId = 1, CreatedAt = now });
        await db.SaveChangesAsync();
        return (db, new CollectionService(db));
    }

    private static SaveCollectionRequest Automated(string match, params CollectionRule[] rules) =>
        new("C", null, null, null, "Automated", match, rules, null, null, true);

    [Fact]
    public async Task Automated_tag_rule_matches_only_active_tagged_products()
    {
        var (db, svc) = await SetupAsync();
        using var _ = db;

        var c = await svc.CreateAsync(Automated("All", new CollectionRule("tag", "eq", "sale")));
        var members = await svc.MembersAsync(c.CollectionId, activeOnly: true);

        Assert.Single(members);
        Assert.Equal(1, members[0].ProductId);   // draft product 3 excluded
    }

    [Fact]
    public async Task Automated_price_rule_filters_by_range()
    {
        var (db, svc) = await SetupAsync();
        using var _ = db;

        var c = await svc.CreateAsync(Automated("All", new CollectionRule("price", "gte", "100")));
        var members = await svc.MembersAsync(c.CollectionId, activeOnly: true);

        Assert.Single(members);
        Assert.Equal(1, members[0].ProductId);
    }

    [Fact]
    public async Task Match_any_unions_rules()
    {
        var (db, svc) = await SetupAsync();
        using var _ = db;

        var c = await svc.CreateAsync(Automated("Any", new CollectionRule("tag", "eq", "summer"), new CollectionRule("tag", "eq", "clearance")));
        var members = await svc.MembersAsync(c.CollectionId, activeOnly: true);

        Assert.Equal(2, members.Count);   // product 1 (summer) + product 2 (clearance)
    }

    [Fact]
    public async Task Manual_membership_is_the_set_list()
    {
        var (db, svc) = await SetupAsync();
        using var _ = db;

        var c = await svc.CreateAsync(new SaveCollectionRequest("Picks", null, null, null, "Manual", "All", null, null, null, true));
        await svc.SetManualMembersAsync(c.CollectionId, new List<long> { 2 });

        var members = await svc.MembersAsync(c.CollectionId, activeOnly: true);
        Assert.Single(members);
        Assert.Equal(2, members[0].ProductId);
    }
}
