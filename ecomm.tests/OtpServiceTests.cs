using ecomm.api.Common.Exceptions;
using ecomm.api.Data.Entities;
using ecomm.api.Features.Auth;
using ecomm.api.Features.Auth.Services;
using ecomm.api.Features.Notifications;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ecomm.tests;

/// <summary>
/// Covers the SMS-channel admin kill switch's effect on mobile-OTP login: when
/// Settings.ChannelSMSEnabled is off (e.g. SMS is blocked on DLT registration), OtpService must
/// fall back to emailing the same code to whatever address the phone's account has on file,
/// rather than silently sending an SMS nobody will ever receive.
/// </summary>
public class OtpServiceTests
{
    private sealed class RecordingSmsSender : ISmsSender
    {
        public int SendCount;
        public Task SendAsync(string phoneNumber, string message, CancellationToken ct = default)
        {
            SendCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingNotificationService : INotificationService
    {
        public string? LastCode;
        public string? LastToEmail;
        public IReadOnlyDictionary<string, string>? LastTokens;
        public int EmailCallCount;

        public Task<bool> SendEmailAsync(string code, string toEmail, IReadOnlyDictionary<string, string> tokens, CancellationToken ct = default)
        {
            EmailCallCount++;
            LastCode = code; LastToEmail = toEmail; LastTokens = tokens;
            return Task.FromResult(true);
        }

        public Task<bool> SendSmsAsync(string code, string toPhone, IReadOnlyDictionary<string, string> tokens, CancellationToken ct = default)
            => throw new NotSupportedException("OtpService never calls this directly — SMS goes through ISmsSender.");
    }

    private static (OtpService svc, RecordingSmsSender sms, RecordingNotificationService notify, ecomm.api.Data.Context.EcommerceDbContext db) New()
    {
        var db = TestDb.New(tenantId: 1);
        var sms = new RecordingSmsSender();
        var notify = new RecordingNotificationService();
        var channelSettings = new NotificationChannelSettings(db);
        var svc = new OtpService(db, new BcryptPasswordHasher(), sms, notify, channelSettings, NullLogger<OtpService>.Instance);
        return (svc, sms, notify, db);
    }

    [Fact]
    public async Task Sms_enabled_by_default_sends_via_sms_and_never_touches_email()
    {
        var (svc, sms, notify, _) = New();

        await svc.RequestAsync("919876543210", "SMS", OtpPurpose.Login);

        Assert.Equal(1, sms.SendCount);
        Assert.Equal(0, notify.EmailCallCount);
    }

    [Fact]
    public async Task Sms_disabled_falls_back_to_the_login_otp_email_template_for_a_known_user()
    {
        var (svc, sms, notify, db) = New();
        db.Users.Add(new User { TenantId = 1, PhoneNumber = "919876543210", Email = "sam@example.com", FullName = "Sam" });
        db.Settings.Add(new Setting { TenantId = 1, SettingKey = "ChannelSMSEnabled", SettingValue = "false", DataType = "string", Category = "Notifications", CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        await svc.RequestAsync("919876543210", "SMS", OtpPurpose.Login);

        Assert.Equal(0, sms.SendCount);
        Assert.Equal(1, notify.EmailCallCount);
        Assert.Equal("LoginOtp", notify.LastCode);
        Assert.Equal("sam@example.com", notify.LastToEmail);
        Assert.Equal("Sam", notify.LastTokens!["CustomerName"]);
        Assert.False(string.IsNullOrWhiteSpace(notify.LastTokens["OtpCode"]));
    }

    [Fact]
    public async Task Sms_disabled_with_no_matching_user_throws_instead_of_silently_dropping_the_code()
    {
        var (svc, _, _, db) = New();
        db.Settings.Add(new Setting { TenantId = 1, SettingKey = "ChannelSMSEnabled", SettingValue = "false", DataType = "string", Category = "Notifications", CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<AppException>(() => svc.RequestAsync("919876543210", "SMS", OtpPurpose.Login));
    }

    [Fact]
    public async Task Sms_disabled_with_a_user_but_no_email_on_file_throws()
    {
        var (svc, _, _, db) = New();
        db.Users.Add(new User { TenantId = 1, PhoneNumber = "919876543210", Email = null });
        db.Settings.Add(new Setting { TenantId = 1, SettingKey = "ChannelSMSEnabled", SettingValue = "false", DataType = "string", Category = "Notifications", CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<AppException>(() => svc.RequestAsync("919876543210", "SMS", OtpPurpose.Login));
    }
}
