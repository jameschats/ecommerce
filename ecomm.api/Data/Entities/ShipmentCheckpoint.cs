namespace ecomm.api.Data.Entities;

/// <summary>
/// One courier tracking scan, append-only. Every webhook is recorded — including statuses we
/// cannot map — because the courier never resends history, so anything discarded is gone for good.
/// <see cref="RawPayload"/> is kept only for unmapped statuses, to improve the mapping later.
/// </summary>
public class ShipmentCheckpoint : ITenantScoped
{
    public long ShipmentCheckpointId { get; set; }
    public long TenantId { get; set; }
    public long ShipmentId { get; set; }

    /// <summary>Exactly what the courier sent, unmodified.</summary>
    public string RawStatus { get; set; } = string.Empty;

    /// <summary>Our lifecycle value, or null when the status wasn't recognised.</summary>
    public string? MappedStatus { get; set; }

    public string? Location { get; set; }
    public string? Remark { get; set; }

    /// <summary>The courier's own timestamp when it supplies one; otherwise null and <see cref="CreatedAt"/> stands in.</summary>
    public DateTime? OccurredAt { get; set; }

    public string? RawPayload { get; set; }
    public DateTime CreatedAt { get; set; }
}
