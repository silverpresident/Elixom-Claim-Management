namespace ElixomClaim.Lib.Entities;

public sealed class OperationRecord
{
    public long Id { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string OperationType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Details { get; set; }
    public string ActorUserId { get; set; } = string.Empty;
    public DateTime ExecutedAtUtc { get; set; }
}
