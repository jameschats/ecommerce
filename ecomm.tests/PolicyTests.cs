using ecomm.api.Common.Exceptions;
using ecomm.api.Features.Policies;
using Xunit;

namespace ecomm.tests;

public class PolicyTests
{
    [Fact]
    public async Task Save_sanitizes_html_and_publishes()
    {
        using var db = TestDb.New(tenantId: 1);
        var svc = new PolicyService(db);

        var saved = await svc.SaveAsync("privacy",
            new SavePolicyRequest("Our Privacy", "<p>We respect you.</p><script>steal()</script>"));

        Assert.True(saved.HasContent);
        Assert.Contains("<p>We respect you.</p>", saved.BodyHtml);
        Assert.DoesNotContain("<script", saved.BodyHtml);

        // Public read returns it (has content) and it appears in the footer link list.
        Assert.NotNull(await svc.GetPublicAsync("privacy"));
        Assert.Contains(await svc.PublicLinksAsync(), l => l.Handle == "privacy" && l.Title == "Our Privacy");
    }

    [Fact]
    public async Task Empty_policy_is_not_public()
    {
        using var db = TestDb.New(tenantId: 1);
        var svc = new PolicyService(db);

        var list = await svc.ListAsync();
        Assert.All(list, p => Assert.False(p.HasContent));   // fresh store: all empty
        Assert.Null(await svc.GetPublicAsync("refund"));
        Assert.Empty(await svc.PublicLinksAsync());
    }

    [Fact]
    public async Task Unknown_handle_is_rejected()
    {
        using var db = TestDb.New(tenantId: 1);
        var svc = new PolicyService(db);
        await Assert.ThrowsAsync<AppException>(() => svc.SaveAsync("nope", new SavePolicyRequest("x", "<p>y</p>")));
    }
}
