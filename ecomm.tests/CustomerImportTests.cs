using System.Text;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Customers;
using Xunit;

namespace ecomm.tests;

public class CustomerImportTests
{
    private static void SeedCustomerRole(ecomm.api.Data.Context.EcommerceDbContext db)
    {
        db.Roles.Add(new Role { RoleId = 1, TenantId = 1, Name = "Customer", NormalizedName = "CUSTOMER" });
        db.SaveChanges();
    }

    private static Stream Csv(string body)
        => new MemoryStream(Encoding.UTF8.GetBytes(body));

    [Fact]
    public async Task Import_creates_new_updates_by_email_merges_tags_skips_blank()
    {
        using var db = TestDb.New(tenantId: 1);
        SeedCustomerRole(db);
        // Existing customer with a tag already set.
        db.Users.Add(new User { UserId = 5, TenantId = 1, Email = "alice@x.com", NormalizedEmail = "ALICE@X.COM", FullName = "Alice" });
        db.UserRoles.Add(new UserRole { UserId = 5, RoleId = 1 });
        db.CustomerProfiles.Add(new CustomerProfile { UserId = 5, TenantId = 1, Tags = "vip", CreatedAt = DateTime.UtcNow });
        db.SaveChanges();

        var csv = "name,email,phone,tags,email marketing\n"
                + "Alice Updated,alice@x.com,,wholesale,yes\n"
                + "Bob,bob@x.com,123,new,no\n"
                + "Nobody,,,,\n";   // has a name but no email/phone -> skipped with a reason
        var svc = new CustomerAdminService(db);

        var result = await svc.ImportAsync(Csv(csv));

        Assert.Equal(3, result.Total);
        Assert.Equal(1, result.Created);   // Bob
        Assert.Equal(1, result.Updated);   // Alice
        Assert.Equal(1, result.Skipped);   // blank row

        var alice = await svc.GetAsync(5);
        Assert.Equal("Alice Updated", alice.FullName);
        Assert.True(alice.AcceptsEmailMarketing);
        Assert.Equal(new[] { "vip", "wholesale" }, alice.Tags);   // merged, not wiped
    }

    [Fact]
    public async Task Tag_filter_and_tag_counts_reflect_imported_customers()
    {
        using var db = TestDb.New(tenantId: 1);
        SeedCustomerRole(db);
        var svc = new CustomerAdminService(db);
        await svc.ImportAsync(Csv(
            "name,email,tags\n" +
            "Alice,alice@x.com,\"vip,wholesale\"\n" +
            "Bob,bob@x.com,wholesale\n" +
            "Cara,cara@x.com,vip\n"));

        var tags = await svc.TagsAsync();
        Assert.Equal(2, tags.Count);
        Assert.Equal("vip", tags[0].Tag);          // 2 uses, sorted first
        Assert.Equal(2, tags[0].Count);
        Assert.Equal(2, tags.First(t => t.Tag == "wholesale").Count);

        var wholesale = await svc.ListAsync(null, null, "wholesale", 1, 20);
        Assert.Equal(2, wholesale.TotalCount);     // Alice + Bob
        Assert.DoesNotContain(wholesale.Items, c => c.Email == "cara@x.com");
    }
}
