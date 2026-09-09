namespace ElixomClaim.Lib.Entities;

public class AuditRecord
{
    public Guid Id { get; set; }
    public string? ActorUserId { get; set; }
    public string? ActorEmail { get; set; }
    public string? CorrelationId { get; set; }
    public string? IpAddress { get; set; }
    public string Action { get; set; } = string.Empty;
    /// <summary>Stable domain type of the audited entity (for example, JobPayment).</summary>
    public string EntityType { get; set; } = string.Empty;
    /// <summary>Stable identifier of the audited entity, stored as text for heterogeneous entities.</summary>
    public string EntityId { get; set; } = string.Empty;
    public string? BeforeStateJson { get; set; }
    public string? AfterStateJson { get; set; }
    public bool IsMcpOperation { get; set; }
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;

    // Compatibility only for callers compiled against the pre-Sprint-13 model; never persisted.
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    [Obsolete("Use EntityType and EntityId.")]
    public string Target { get => $"{EntityType}:{EntityId}"; set { EntityType = value.Split(':', 2)[0]; EntityId = value.Contains(':') ? value.Split(':', 2)[1] : value; } }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    [Obsolete("Use OccurredAtUtc.")]
    public DateTime TimestampUtc { get => OccurredAtUtc; set => OccurredAtUtc = value; }
}
