namespace ElixomClaim.Lib.Entities;

public enum EmailOutboxStatus
{
    Pending,
    Processing,
    Sent,
    Failed,
    SkippedInvalidRecipient
}

public class EmailOutboxItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Recipient { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string HtmlBody { get; set; } = string.Empty;
    public string RelatedEntityType { get; set; } = string.Empty;
    public string RelatedEntityId { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public EmailOutboxStatus Status { get; set; } = EmailOutboxStatus.Pending;
    public int AttemptCount { get; set; }
    public DateTime AvailableAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? SentAtUtc { get; set; }
    public string? FailureReason { get; set; }
}
