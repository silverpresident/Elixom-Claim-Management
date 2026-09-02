namespace ElixomClaim.Lib.Entities;

public class AuditRecord
{
    public long Id { get; set; }
    public string? ActorUserId { get; set; }
    public string? ActorEmail { get; set; }
    public string? CorrelationId { get; set; }
    public string? IpAddress { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public string? BeforeStateJson { get; set; }
    public string? AfterStateJson { get; set; }
    public bool IsMcpOperation { get; set; }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}
