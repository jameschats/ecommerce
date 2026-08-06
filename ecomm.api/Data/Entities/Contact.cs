namespace ecomm.api.Data.Entities;

/// <summary>
/// A visitor enquiry. The contact form writes these; the admin inbox works through them.
/// Also the list a campaign sends to, which is why consent is recorded here.
/// </summary>
public class Contact
{
    public long ContactId { get; set; }
    public long TenantId { get; set; } = 1;

    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Subject { get; set; }
    public string? Message { get; set; }

    /// <summary>ContactForm | Order | Import — how this person reached the list.</summary>
    public string Source { get; set; } = "ContactForm";
    public string? SourcePage { get; set; }

    /// <summary>New | Open | Closed | Spam.</summary>
    public string Status { get; set; } = "New";
    public string? AdminNotes { get; set; }

    /// <summary>Set when a signed-in visitor writes in; null for anonymous senders.</summary>
    public long? UserId { get; set; }

    /// <summary>
    /// Marketing consent. False unless explicitly given — asking a question is not asking
    /// to be mailed.
    /// </summary>
    public bool SubscribedToEmails { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
