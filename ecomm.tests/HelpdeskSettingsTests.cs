using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Support;
using Xunit;

namespace ecomm.tests;

public class HelpdeskSettingsTests
{
    [Fact]
    public async Task Defaults_to_enabled_with_no_active_hours_restriction()
    {
        using var db = TestDb.New(tenantId: 1);
        var svc = new HelpdeskSettingsService(db);

        var settings = await svc.GetAsync();

        Assert.True(settings.ChatbotEnabled);
        Assert.Null(settings.ActiveHoursStart);
        Assert.Null(settings.ActiveHoursEnd);
    }

    [Fact]
    public async Task Update_round_trips_disabled_and_active_hours()
    {
        using var db = TestDb.New(tenantId: 1);
        var svc = new HelpdeskSettingsService(db);

        var settings = await svc.UpdateAsync(new UpdateHelpdeskSettingsRequest(false, "09:00", "18:00"));

        Assert.False(settings.ChatbotEnabled);
        Assert.Equal("09:00", settings.ActiveHoursStart);
        Assert.Equal("18:00", settings.ActiveHoursEnd);
    }

    [Fact]
    public async Task Invalid_active_hours_are_rejected()
    {
        using var db = TestDb.New(tenantId: 1);
        var svc = new HelpdeskSettingsService(db);

        await Assert.ThrowsAsync<AppException>(() => svc.UpdateAsync(new UpdateHelpdeskSettingsRequest(true, "not-a-time", "18:00")));
    }

    [Fact]
    public async Task One_sided_active_hours_are_rejected()
    {
        using var db = TestDb.New(tenantId: 1);
        var svc = new HelpdeskSettingsService(db);

        await Assert.ThrowsAsync<AppException>(() => svc.UpdateAsync(new UpdateHelpdeskSettingsRequest(true, "09:00", null)));
    }

    [Fact]
    public async Task Unanswered_questions_are_listed_newest_first()
    {
        using var db = TestDb.New(tenantId: 1);
        db.ChatbotUnansweredQuestions.Add(new ChatbotUnansweredQuestion { TenantId = 1, Question = "first", CreatedAt = DateTime.UtcNow.AddMinutes(-5) });
        db.ChatbotUnansweredQuestions.Add(new ChatbotUnansweredQuestion { TenantId = 1, Question = "second", CreatedAt = DateTime.UtcNow });
        db.SaveChanges();
        var svc = new HelpdeskSettingsService(db);

        var list = await svc.UnansweredAsync();

        Assert.Equal("second", list[0].Question);
        Assert.Equal("first", list[1].Question);
    }

    [Fact]
    public async Task Settings_are_scoped_per_tenant()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db1 = TestDb.ForDatabase(dbName, tenantId: 1);
        await new HelpdeskSettingsService(db1).UpdateAsync(new UpdateHelpdeskSettingsRequest(false, null, null));

        using var db2 = TestDb.ForDatabase(dbName, tenantId: 2);
        var settings = await new HelpdeskSettingsService(db2).GetAsync();

        Assert.True(settings.ChatbotEnabled);   // tenant 2 never touched its own setting, and can't see tenant 1's
    }
}
