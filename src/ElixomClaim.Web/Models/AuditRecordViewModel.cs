namespace ElixomClaim.Web.Models;

public class AuditRecordViewModel
{
    public Guid Id { get; set; }
    public string? ActorEmail { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string? CorrelationId { get; set; }
    public string? IpAddress { get; set; }
    public bool IsMcpOperation { get; set; }
    public DateTime OccurredAtUtc { get; set; }

    // Only present for Administrator
    public string? BeforeStateJson { get; set; }
    public string? AfterStateJson { get; set; }
}
