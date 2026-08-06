namespace ecomm.api.Data.Entities;

/// <summary>A promotional email send. Audience is resolved to recipient rows when it starts.</summary>
public class Campaign
{
    public long CampaignId { get; set; }
    public long TenantId { get; set; } = 1;

    public string Name { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;

    /// <summary>Contacts | Customers | Both.</summary>
    public string Audience { get; set; } = "Contacts";

    /// <summary>Draft | Sending | Sent | Failed.</summary>
    public string Status { get; set; } = "Draft";

    public int TotalRecipients { get; set; }
    public int SentCount { get; set; }
    public int FailedCount { get; set; }

    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// One address on one campaign. The unique index on (CampaignId, Email) is what makes a
/// resumed send safe — nobody can be mailed twice.
/// </summary>
public class CampaignRecipient
{
    public long CampaignRecipientId { get; set; }
    public long CampaignId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? Name { get; set; }

    /// <summary>Pending | Sent | Failed.</summary>
    public string Status { get; set; } = "Pending";
    public string? Error { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
