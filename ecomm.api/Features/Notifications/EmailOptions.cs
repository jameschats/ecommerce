namespace ecomm.api.Features.Notifications;

/// <summary>Bound from the <c>Email</c> config section. Provider selects the <c>IEmailSender</c> impl.</summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary><c>Logging</c> (dev — writes to the log) or <c>Smtp</c> (real delivery).</summary>
    public string Provider { get; set; } = "Logging";
    public string FromAddress { get; set; } = "no-reply@calendarshop.online";
    public string FromName { get; set; } = "CalendarShop";

    // SMTP settings (used only when Provider = Smtp)
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public bool UseSsl { get; set; } = true;
}
