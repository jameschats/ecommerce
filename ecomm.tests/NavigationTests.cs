using ecomm.api.Common.Exceptions;
using ecomm.api.Features.Navigation;
using Xunit;

namespace ecomm.tests;

public class NavigationTests
{
    [Fact]
    public async Task Save_and_get_menu_filters_blank_items()
    {
        using var db = TestDb.New(tenantId: 1);
        var svc = new NavigationService(db);

        await svc.SaveMenuAsync("main-menu", new SaveMenuRequest(new List<MenuItemDto>
        {
            new("Shop", "/products"), new("", "/blank"),
        }));

        var m = await svc.GetMenuAsync("main-menu");
        Assert.Single(m.Items);                 // blank-label item dropped
        Assert.Equal("Shop", m.Items[0].Label);
    }

    [Fact]
    public async Task Unknown_menu_handle_is_rejected()
    {
        using var db = TestDb.New(tenantId: 1);
        var svc = new NavigationService(db);
        await Assert.ThrowsAsync<AppException>(() => svc.GetMenuAsync("nope"));
    }

    [Fact]
    public async Task Redirect_normalizes_paths_and_resolves()
    {
        using var db = TestDb.New(tenantId: 1);
        var svc = new NavigationService(db);

        var r = await svc.CreateRedirectAsync(new SaveRedirectRequest("old-page/", "/new-page"));
        Assert.Equal("/old-page", r.FromPath);           // leading slash added, trailing removed

        Assert.Equal("/new-page", await svc.ResolveRedirectAsync("/old-page"));
        Assert.Null(await svc.ResolveRedirectAsync("/missing"));
    }

    [Fact]
    public async Task Redirect_to_itself_is_rejected()
    {
        using var db = TestDb.New(tenantId: 1);
        var svc = new NavigationService(db);
        await Assert.ThrowsAsync<AppException>(() => svc.CreateRedirectAsync(new SaveRedirectRequest("/a", "/a")));
    }
}
