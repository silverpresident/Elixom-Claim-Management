namespace ElixomClaim.Lib.Entities;

public enum JobPaymentStatus
{
    Processing,
    Submitted,
    Scheduled,
    Paid
}

public enum PayrollStatus
{
    Generated,
    Submitted,
    Paid
}

/// <summary>Minimal payroll record introduced as the Sprint 04 association prerequisite. Salary calculation belongs to Sprint 05.</summary>
public class Payroll
{
    public long Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public decimal NetAmount { get; set; }
    public PayrollStatus Status { get; set; } = PayrollStatus.Generated;
    public bool IsLocked { get; set; }
    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

public class JobPayment
{
    public long Id { get; set; }
    public Guid? PayeeUserId { get; set; }
    public User? PayeeUser { get; set; }
    public Guid? CollectionClientId { get; set; }
    public CollectionClient? CollectionClient { get; set; }
    public JobPaymentStatus Status { get; set; } = JobPaymentStatus.Processing;
    public string? PublicNote { get; set; }
    public string? InternalNote { get; set; }
    public decimal JobTotal { get; set; }
    public decimal ClientProcessingFee { get; set; }
    public decimal TotalTxnProcessingFee { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal TotalPaid { get; set; }
    public string Currency { get; set; } = "JMD";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? SubmittedAtUtc { get; set; }
    public DateTime? ScheduledAtUtc { get; set; }
    public DateTime? PaymentDateUtc { get; set; }
    public string? PaymentTransactionNumber { get; set; }
    public long? OriginalJobPaymentId { get; set; }
    public JobPayment? OriginalJobPayment { get; set; }
    public bool IsAdjustment { get; set; }
    public bool IsRecoveryReceivable { get; set; }
    public string? AdjustmentReason { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ICollection<JobPaymentClaim> Claims { get; set; } = new List<JobPaymentClaim>();
    public ICollection<JobPaymentCollection> Collections { get; set; } = new List<JobPaymentCollection>();
    public ICollection<JobPaymentPayroll> Payrolls { get; set; } = new List<JobPaymentPayroll>();
    public ICollection<JobPaymentDeduction> Deductions { get; set; } = new List<JobPaymentDeduction>();
}

public class JobPaymentClaim { public long JobPaymentId { get; set; } public JobPayment JobPayment { get; set; } = null!; public long ClaimId { get; set; } public Claim Claim { get; set; } = null!; }
public class JobPaymentCollection { public long JobPaymentId { get; set; } public JobPayment JobPayment { get; set; } = null!; public long CollectionTransactionId { get; set; } public CollectionTransaction CollectionTransaction { get; set; } = null!; }
public class JobPaymentPayroll { public long JobPaymentId { get; set; } public JobPayment JobPayment { get; set; } = null!; public long PayrollId { get; set; } public Payroll Payroll { get; set; } = null!; }
public class JobPaymentDeduction { public long Id { get; set; } public long JobPaymentId { get; set; } public JobPayment JobPayment { get; set; } = null!; public string Description { get; set; } = string.Empty; public decimal Amount { get; set; } public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow; }
