using ecomm.api.Data.Context;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ecomm.api.Features.Notifications;

/// <summary>
/// Builds the effective <see cref="IEmailSender"/> at request time. Config precedence: the
/// <c>PlatformEmailSettings</c> row (set in super-admin) wins; otherwise the app-wide <c>Email</c>
/// env config; otherwise the dev <see cref="LoggingEmailSender"/>. Scoped (reads the DB), mirroring
/// <see cref="Payments.PlatformPaymentGatewayFactory"/> — this is what makes the Mock/Live toggle and
/// SMTP credential changes take effect with no restart. Password is decrypted here (DataProtection).
/// </summary>
public sealed class EmailSenderFactory(
    IOptions<EmailOptions> options,
    EcommerceDbContext db,
    IDataProtectionProvider dp,
    ILogger<LoggingEmailSender> logLogger)
{
    /// <summary>DataProtection purpose for the platform SMTP password at rest.</summary>
    public const string ProtectorPurpose = "PlatformEmailSettings.v1";

    public IEmailSender Create()
    {
        var o = Resolve();
        if (o.Provider.Equals("Smtp", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(o.Host))
            return new SmtpEmailSender(o);
        return new LoggingEmailSender(logLogger);   // "Mock" / Logging / misconfigured -> nothing is sent
    }

    /// <summary>Non-secret view for the super-admin GET: what's in force + whether a password is stored + its source.</summary>
    public (string Provider, string? Host, int Port, string? Username, string? FromAddress, string? FromName, bool UseSsl, bool HasSecret, string Source) Describe()
    {
        var row = db.PlatformEmailSettings.AsNoTracking().FirstOrDefault();
        var o = Resolve();
        return (o.Provider, o.Host, o.Port, o.Username, o.FromAddress, o.FromName, o.UseSsl,
            !string.IsNullOrWhiteSpace(o.Password), row is null ? "env" : "console");
    }

    private EmailOptions Resolve()
    {
        var row = db.PlatformEmailSettings.AsNoTracking().FirstOrDefault();
        if (row is null) return options.Value;   // env fallback

        string? password = null;
        if (!string.IsNullOrEmpty(row.Password))
        {
            // Corrupt/rotated key material must not take the whole notification pipeline down.
            try { password = dp.CreateProtector(ProtectorPurpose).Unprotect(row.Password); }
            catch { password = null; }
        }
        return new EmailOptions
        {
            Provider = row.Provider,
            Host = row.Host ?? "",
            Port = row.Port,
            Username = row.Username ?? "",
            Password = password ?? "",
            FromAddress = string.IsNullOrWhiteSpace(row.FromAddress) ? options.Value.FromAddress : row.FromAddress,
            FromName = string.IsNullOrWhiteSpace(row.FromName) ? options.Value.FromName : row.FromName,
            UseSsl = row.UseSsl,
        };
    }
}
