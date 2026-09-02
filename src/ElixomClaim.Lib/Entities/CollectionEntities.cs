namespace ElixomClaim.Lib.Entities;

public enum CollectionMethod
{
    Cash,
    Pos,
    BankTransfer,
    CreditNote
}

public enum CollectionStatus
{
    Collected,
    Processing,
    Transferred
}

public class CollectionClient
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<CollectionClientUser> AssignedUsers { get; set; } = new List<CollectionClientUser>();
    public ICollection<CollectionClientBankDetail> BankDetails { get; set; } = new List<CollectionClientBankDetail>();
    public ICollection<CollectionPurposeOption> PurposeOptions { get; set; } = new List<CollectionPurposeOption>();
    public ICollection<CollectionAmountOption> AmountOptions { get; set; } = new List<CollectionAmountOption>();
}

public class CollectionClientUser
{
    public Guid CollectionClientId { get; set; }
    public CollectionClient CollectionClient { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public DateTime AssignedAtUtc { get; set; } = DateTime.UtcNow;
}

public class CollectionClientBankDetail
{
    public long Id { get; set; }
    public Guid CollectionClientId { get; set; }
    public CollectionClient CollectionClient { get; set; } = null!;
    public string AccountName { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string BranchCode { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class CollectionPurposeOption
{
    public long Id { get; set; }
    public Guid CollectionClientId { get; set; }
    public CollectionClient CollectionClient { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
}

public class CollectionAmountOption
{
    public long Id { get; set; }
    public Guid CollectionClientId { get; set; }
    public CollectionClient CollectionClient { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
}

public class CollectionTransaction
{
    public long Id { get; set; }
    public Guid CollectionClientId { get; set; }
    public CollectionClient CollectionClient { get; set; } = null!;
    public long PurposeOptionId { get; set; }
    public CollectionPurposeOption PurposeOption { get; set; } = null!;
    public long AmountOptionId { get; set; }
    public CollectionAmountOption AmountOption { get; set; } = null!;
    public Guid TellerUserId { get; set; }
    public User TellerUser { get; set; } = null!;
    public string PayorName { get; set; } = string.Empty;
    public string? PayorEmail { get; set; }
    public string? ReferenceNumber { get; set; }
    public CollectionMethod Method { get; set; }
    public CollectionStatus Status { get; set; } = CollectionStatus.Collected;
    public decimal Amount { get; set; }
    public decimal ProcessingFee { get; set; }
    public string Currency { get; set; } = "JMD";
    public DateTime PaymentDateUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
