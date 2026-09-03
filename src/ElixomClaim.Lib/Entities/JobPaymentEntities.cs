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

public enum SalaryAdjustmentType
{
    Benefit,
    Deduction
}

public enum PayrollEntryType
{
    Base,
    Benefit,
    Deduction,
    Custom
}

public class SalaryDefinition
{
    public long Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string Description { get; set; } = string.Empty;
    public decimal BaseAmount { get; set; }
    public DateOnly FirstSalaryDate { get; set; }
    public DateOnly LastSalaryDate { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public int RecurrenceDays { get; set; }
    public int RecurrenceMonths { get; set; }
    public DayOfWeek NearestWeekday { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public ICollection<SalaryAdjustment> Adjustments { get; set; } = new List<SalaryAdjustment>();
    public ICollection<Payroll> Payrolls { get; set; } = new List<Payroll>();
}

public class SalaryAdjustment
{
    public long Id { get; set; }
    public long SalaryDefinitionId { get; set; }
    public SalaryDefinition SalaryDefinition { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public decimal PercentageRate { get; set; }
    public decimal FixedValue { get; set; }
    public SalaryAdjustmentType Type { get; set; }
}

public class Payroll
{
    public long Id { get; set; }
    public long SalaryDefinitionId { get; set; }
    public SalaryDefinition SalaryDefinition { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public DateOnly PeriodEndingDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal PayrollTotal { get; set; }
    public PayrollStatus Status { get; set; } = PayrollStatus.Generated;
    public bool IsLocked { get; set; }
    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? SubmittedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public ICollection<PayrollEntry> Entries { get; set; } = new List<PayrollEntry>();
}

public class PayrollEntry
{
    public long Id { get; set; }
    public long PayrollId { get; set; }
    public Payroll Payroll { get; set; } = null!;
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public PayrollEntryType Type { get; set; }
    public bool IsLocked { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
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
