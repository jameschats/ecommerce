using ecomm.api.Data.Context;
using ecomm.api.Features.MarketingStudio;
using Xunit;

namespace ecomm.tests;

public class MarketingPlanSettingsTests
{
    /// <summary>Stub connection service: reports a fixed set of connected platforms.</summary>
    private sealed class FakeConnections(params string[] connectedPlatforms) : ISocialConnectionService
    {
        public Task<IReadOnlyList<SocialConnectionDto>> ListAsync(CancellationToken ct = default)
        {
            var set = new HashSet<string>(connectedPlatforms, StringComparer.OrdinalIgnoreCase);
            IReadOnlyList<SocialConnectionDto> list = SocialPlatforms.All.Select(p =>
                new SocialConnectionDto(p.Key, p.DisplayName, set.Contains(p.Key) ? "connected" : "not_connected",
                    null, set.Contains(p.Key), null, null)).ToList();
            return Task.FromResult(list);
        }
        public Task<StartConnectResult> StartAsync(string platform, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<SocialConnectionDto> CompleteAsync(string platform, string code, string state, CancellationToken ct = default) => throw new NotImplementedException();
        public Task DisconnectAsync(string platform, CancellationToken ct = default) => Task.CompletedTask;
    }

    private static MarketingPlanSettingsService New(EcommerceDbContext db, params string[] connected) =>
        new(db, new FakeConnections(connected));

    [Fact]
    public async Task Get_returns_sensible_defaults_and_only_connected_channels()
    {
        using var db = TestDb.New(tenantId: 1);
        var dto = await New(db, "linkedin", "pinterest").GetAsync();

        Assert.Equal(3, dto.TextPerWeek);
        Assert.Equal(2, dto.PostersPerWeek);
        Assert.Equal(1, dto.WeekStartDay);
        Assert.Equal(2, dto.Channels.Count);                       // only the two connected
        Assert.Contains(dto.Channels, c => c.Platform == "linkedin" && c.AllowPoster);
        Assert.DoesNotContain(dto.Channels, c => c.Platform == "youtube");
    }

    [Fact]
    public async Task Get_with_no_connections_returns_an_empty_channel_matrix()
    {
        using var db = TestDb.New(tenantId: 1);
        var dto = await New(db).GetAsync();
        Assert.Empty(dto.Channels);
    }

    [Fact]
    public async Task Save_persists_cadence_and_channel_toggles()
    {
        using var db = TestDb.New(tenantId: 1);
        var svc = New(db, "linkedin", "instagram");

        var input = new MarketingPlanSettingsDto(5, 4, 1, 0, 18, true, new[]
        {
            new ChannelPrefDto("linkedin", "LinkedIn", true, true, true, true, false),
            new ChannelPrefDto("instagram", "Instagram", true, true, false, true, true),   // no text on IG
        });
        await svc.SaveAsync(input);
        var dto = await svc.GetAsync();

        Assert.Equal(5, dto.TextPerWeek);
        Assert.True(dto.AutoRecur);
        Assert.Equal(18, dto.DefaultPostHour);
        Assert.False(dto.Channels.Single(c => c.Platform == "linkedin").AllowVideo);
        Assert.False(dto.Channels.Single(c => c.Platform == "instagram").AllowText);
    }

    [Fact]
    public async Task Save_clamps_out_of_range_values()
    {
        using var db = TestDb.New(tenantId: 1);
        var svc = New(db, "linkedin");
        var dto = await svc.SaveAsync(new MarketingPlanSettingsDto(999, -5, 3, 9, 30, false, Array.Empty<ChannelPrefDto>()));

        Assert.Equal(50, dto.TextPerWeek);        // capped
        Assert.Equal(0, dto.PostersPerWeek);      // floored
        Assert.InRange(dto.WeekStartDay, 0, 6);
        Assert.InRange(dto.DefaultPostHour, 0, 23);
    }

    [Fact]
    public async Task Save_upserts_the_same_settings_row()
    {
        using var db = TestDb.New(tenantId: 1);
        var svc = New(db, "linkedin");
        await svc.SaveAsync(new MarketingPlanSettingsDto(1, 1, 0, 1, 10, false, Array.Empty<ChannelPrefDto>()));
        await svc.SaveAsync(new MarketingPlanSettingsDto(7, 1, 0, 1, 10, false, Array.Empty<ChannelPrefDto>()));

        Assert.Equal(1, db.MarketingPlanSettings.Count());
        Assert.Equal(7, (await svc.GetAsync()).TextPerWeek);
    }
}
