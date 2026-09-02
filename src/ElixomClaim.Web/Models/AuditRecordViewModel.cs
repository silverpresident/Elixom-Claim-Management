namespace ElixomClaim.Web.Models;

public class AuditRecordViewModel
{
    public long Id { get; set; }
    public string? ActorEmail { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public string? CorrelationId { get; set; }
    public string? IpAddress { get; set; }
    public bool IsMcpOperation { get; set; }
    public DateTime TimestampUtc { get; set; }

    // Only present for Administrator
    public string? BeforeStateJson { get; set; }
    public string? AfterStateJson { get; set; }
}
