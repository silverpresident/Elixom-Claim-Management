namespace ElixomClaim.Lib.Entities;

public class Claim
{
    public long Id { get; set; }
    public Guid ClaimantUserId { get; set; }
    public User ClaimantUser { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; } // JMD exact
    public string Currency { get; set; } = "JMD";
    public ClaimStatus Status { get; set; } = ClaimStatus.Draft;
    public ClaimPaymentStatus PaymentStatus { get; set; } = ClaimPaymentStatus.Unpaid;
    public string? RejectionReason { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ICollection<ClaimComment> Comments { get; set; } = new List<ClaimComment>();
}

public class ClaimComment
{
    public long Id { get; set; }
    public long ClaimId { get; set; }
    public Claim Claim { get; set; } = null!;
    public Guid AuthorUserId { get; set; }
    public User AuthorUser { get; set; } = null!;
    public string Content { get; set; } = string.Empty;
    public bool IsPrivate { get; set; } // Private comments visible to Manager/Accountant/Admin only
    public bool IsDeleted { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
