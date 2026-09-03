using ElixomClaim.Lib.Common;
using ElixomClaim.Lib.Data;
using ElixomClaim.Lib.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ElixomClaim.Lib.Services;

public interface ISalaryPayrollService
{
    Task<Result<SalaryDefinition>> CreateDefinitionAsync(CreateSalaryDefinitionCommand command, CancellationToken cancellationToken = default);
    Task<Result<SalaryPayrollPreview>> PreviewAsync(long salaryDefinitionId, Guid actorUserId, DateOnly asOfDate, CancellationToken cancellationToken = default);
    Task<Result<Payroll>> GenerateForDefinitionAsync(long salaryDefinitionId, Guid actorUserId, DateOnly asOfDate, CancellationToken cancellationToken = default);
    Task<Result> AddCustomEntryAsync(long payrollId, Guid actorUserId, string description, decimal amount, CancellationToken cancellationToken = default);
    Task<Result<JobPayment>> SubmitAsync(long payrollId, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<int> GenerateDueAsync(DateOnly asOfDate, CancellationToken cancellationToken = default);
}

public sealed record CreateSalaryDefinitionCommand(Guid ActorUserId, Guid UserId, string Description, decimal BaseAmount, DateOnly FirstSalaryDate, DateOnly StartDate, DateOnly? EndDate, int RecurrenceDays, int RecurrenceMonths, DayOfWeek NearestWeekday);

public sealed record SalaryPayrollPreview(DateOnly DueDate, SalaryGenerationEligibility Eligibility, decimal ProjectedTotal);

public sealed class SalaryPayrollService : ISalaryPayrollService
{
    private readonly ApplicationDbContext _db;
    private readonly ISalaryRecurrencePlanner _planner;
    private readonly IAuditService _audit;
    private readonly ISystemClock _clock;
    private readonly ILogger<SalaryPayrollService> _logger;

    public SalaryPayrollService(ApplicationDbContext db, ISalaryRecurrencePlanner planner, IAuditService audit, ISystemClock clock, ILogger<SalaryPayrollService> logger)
    {
        _db = db;
        _planner = planner;
        _audit = audit;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Result<SalaryDefinition>> CreateDefinitionAsync(CreateSalaryDefinitionCommand command, CancellationToken cancellationToken = default)
    {
        if (!await IsAccountantAsync(command.ActorUserId, cancellationToken)) return Result.Failure<SalaryDefinition>("Accountant access is required.");
        if (string.IsNullOrWhiteSpace(command.Description) || command.BaseAmount <= 0 || command.RecurrenceDays < 0 || command.RecurrenceMonths < 0 || (command.RecurrenceDays == 0 && command.RecurrenceMonths == 0) || command.EndDate is { } end && end < command.StartDate) return Result.Failure<SalaryDefinition>("Salary definition values are invalid.");
        if (!await _db.Users.AnyAsync(user => user.Id == command.UserId && user.IsActive, cancellationToken)) return Result.Failure<SalaryDefinition>("Active payee user was not found.");
        var definition = new SalaryDefinition { UserId = command.UserId, Description = command.Description.Trim(), BaseAmount = command.BaseAmount, FirstSalaryDate = command.FirstSalaryDate, LastSalaryDate = command.FirstSalaryDate, StartDate = command.StartDate, EndDate = command.EndDate, RecurrenceDays = command.RecurrenceDays, RecurrenceMonths = command.RecurrenceMonths, NearestWeekday = command.NearestWeekday, CreatedAtUtc = _clock.UtcNow, UpdatedAtUtc = _clock.UtcNow };
        _db.SalaryDefinitions.Add(definition); await _db.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync("SALARY_DEFINITION_CREATED", $"SalaryDefinition:{definition.Id}", afterState: new { definition.Id, definition.UserId, definition.BaseAmount }, actorUserId: command.ActorUserId.ToString(), cancellationToken: cancellationToken);
        return Result.Success(definition);
    }

    public async Task<Result<SalaryPayrollPreview>> PreviewAsync(long salaryDefinitionId, Guid actorUserId, DateOnly asOfDate, CancellationToken cancellationToken = default)
    {
        if (!await IsAccountantAsync(actorUserId, cancellationToken)) return Result.Failure<SalaryPayrollPreview>("Accountant access is required.");
        var definition = await _db.SalaryDefinitions.Include(s => s.Adjustments).SingleOrDefaultAsync(s => s.Id == salaryDefinitionId, cancellationToken);
        if (definition is null) return Result.Failure<SalaryPayrollPreview>("Salary definition was not found.");
        var periods = await _db.Payrolls.Where(p => p.SalaryDefinitionId == salaryDefinitionId).Select(p => p.PeriodEndingDate).ToArrayAsync(cancellationToken);
        var plan = _planner.Plan(definition, asOfDate, periods);
        var total = definition.BaseAmount + definition.Adjustments.Where(a => a.Type == SalaryAdjustmentType.Benefit).Sum(a => definition.BaseAmount * a.PercentageRate + a.FixedValue) - definition.Adjustments.Where(a => a.Type == SalaryAdjustmentType.Deduction).Sum(a => definition.BaseAmount * a.PercentageRate + a.FixedValue);
        return Result.Success(new SalaryPayrollPreview(plan.DueDate, plan.Eligibility, total));
    }

    public async Task<Result<Payroll>> GenerateForDefinitionAsync(long salaryDefinitionId, Guid actorUserId, DateOnly asOfDate, CancellationToken cancellationToken = default)
    {
        var authorized = await IsAccountantAsync(actorUserId, cancellationToken);
        if (!authorized)
            return Result.Failure<Payroll>("Accountant access is required.");

        var definition = await _db.SalaryDefinitions
            .Include(s => s.Adjustments)
            .SingleOrDefaultAsync(s => s.Id == salaryDefinitionId, cancellationToken);
        if (definition is null)
            return Result.Failure<Payroll>("Salary definition was not found.");

        var generatedPeriods = await _db.Payrolls
            .Where(p => p.SalaryDefinitionId == salaryDefinitionId)
            .Select(p => p.PeriodEndingDate)
            .ToArrayAsync(cancellationToken);
        var plan = _planner.Plan(definition, asOfDate, generatedPeriods);
        if (!plan.CanGenerate)
            return Result.Failure<Payroll>($"Salary payroll cannot be generated: {plan.Eligibility}.");

        await using var transaction = _db.Database.IsRelational() ? await _db.Database.BeginTransactionAsync(cancellationToken) : null;
        try
        {
            var payroll = CreatePayroll(definition, plan.DueDate);
            _db.Payrolls.Add(payroll);
            definition.LastSalaryDate = plan.DueDate;
            definition.UpdatedAtUtc = _clock.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            await _audit.LogAsync("PAYROLL_GENERATED", $"Payroll:{payroll.Id}", afterState: new { payroll.Id, payroll.SalaryDefinitionId, payroll.PeriodEndingDate, payroll.PayrollTotal }, actorUserId: actorUserId.ToString(), cancellationToken: cancellationToken);
            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);
            _logger.LogInformation("Generated payroll {PayrollId} for salary definition {SalaryDefinitionId}.", payroll.Id, definition.Id);
            return Result.Success(payroll);
        }
        catch (DbUpdateException exception)
        {
            if (transaction is not null)
                await transaction.RollbackAsync(cancellationToken);
            _logger.LogWarning(exception, "Salary payroll generation conflicted for definition {SalaryDefinitionId}.", salaryDefinitionId);
            return Result.Failure<Payroll>("A payroll for this salary due period already exists.");
        }
    }

    public async Task<int> GenerateDueAsync(DateOnly asOfDate, CancellationToken cancellationToken = default)
    {
        var definitionIds = await _db.SalaryDefinitions.Where(definition => definition.IsActive).Select(definition => definition.Id).ToArrayAsync(cancellationToken);
        var generated = 0;
        foreach (var definitionId in definitionIds)
        {
            var definition = await _db.SalaryDefinitions.Include(s => s.Adjustments).SingleAsync(s => s.Id == definitionId, cancellationToken);
            var periods = await _db.Payrolls.Where(p => p.SalaryDefinitionId == definitionId).Select(p => p.PeriodEndingDate).ToArrayAsync(cancellationToken);
            var plan = _planner.Plan(definition, asOfDate, periods);
            if (!plan.CanGenerate) continue;
            try
            {
                var payroll = CreatePayroll(definition, plan.DueDate);
                _db.Payrolls.Add(payroll); definition.LastSalaryDate = plan.DueDate; definition.UpdatedAtUtc = _clock.UtcNow;
                await _db.SaveChangesAsync(cancellationToken);
                await _audit.LogAsync("PAYROLL_SCHEDULED_GENERATION", $"Payroll:{payroll.Id}", afterState: new { payroll.Id, payroll.SalaryDefinitionId, payroll.PeriodEndingDate }, cancellationToken: cancellationToken);
                generated++;
            }
            catch (DbUpdateException) { _db.ChangeTracker.Clear(); }
        }
        return generated;
    }

    public async Task<Result> AddCustomEntryAsync(long payrollId, Guid actorUserId, string description, decimal amount, CancellationToken cancellationToken = default)
    {
        if (!await IsAccountantAsync(actorUserId, cancellationToken)) return Result.Failure("Accountant access is required.");
        if (string.IsNullOrWhiteSpace(description) || amount == 0) return Result.Failure("A custom entry description and non-zero amount are required.");
        var payroll = await _db.Payrolls.Include(p => p.Entries).SingleOrDefaultAsync(p => p.Id == payrollId, cancellationToken);
        if (payroll is null || payroll.Status != PayrollStatus.Generated || payroll.IsLocked) return Result.Failure("Custom entries can only be added to an unlocked generated payroll.");
        if (amount < 0 && payroll.PayrollTotal + amount < 0) return Result.Failure("A negative custom entry cannot make payroll net pay negative.");
        payroll.Entries.Add(new PayrollEntry { Description = description.Trim(), Amount = amount, Type = PayrollEntryType.Custom, IsLocked = false, SortOrder = payroll.Entries.Count, CreatedAtUtc = _clock.UtcNow });
        payroll.PayrollTotal += amount;
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync("PAYROLL_CUSTOM_ENTRY_ADDED", $"Payroll:{payroll.Id}", afterState: new { payroll.Id, payroll.PayrollTotal }, actorUserId: actorUserId.ToString(), cancellationToken: cancellationToken);
        return Result.Success();
    }

    public async Task<Result<JobPayment>> SubmitAsync(long payrollId, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        if (!await IsAccountantAsync(actorUserId, cancellationToken)) return Result.Failure<JobPayment>("Accountant access is required.");
        var payroll = await _db.Payrolls.Include(p => p.Entries).SingleOrDefaultAsync(p => p.Id == payrollId, cancellationToken);
        if (payroll is null || payroll.Status != PayrollStatus.Generated || payroll.IsLocked) return Result.Failure<JobPayment>("Only an unlocked generated payroll can be submitted.");
        if (payroll.PayrollTotal < 0) return Result.Failure<JobPayment>("Payroll net pay cannot be negative.");
        if (await _db.JobPaymentPayrolls.AnyAsync(link => link.PayrollId == payrollId, cancellationToken)) return Result.Failure<JobPayment>("Payroll is already bound to a job payment.");
        await using var transaction = _db.Database.IsRelational() ? await _db.Database.BeginTransactionAsync(cancellationToken) : null;
        try
        {
            payroll.Status = PayrollStatus.Submitted; payroll.IsLocked = true; payroll.SubmittedAtUtc = _clock.UtcNow;
            foreach (var entry in payroll.Entries) entry.IsLocked = true;
            var jobPayment = new JobPayment { PayeeUserId = payroll.UserId, Status = JobPaymentStatus.Processing, JobTotal = payroll.PayrollTotal, TotalPaid = payroll.PayrollTotal, Currency = "JMD", CreatedAtUtc = _clock.UtcNow };
            _db.JobPayments.Add(jobPayment); await _db.SaveChangesAsync(cancellationToken);
            _db.JobPaymentPayrolls.Add(new JobPaymentPayroll { JobPaymentId = jobPayment.Id, PayrollId = payroll.Id }); await _db.SaveChangesAsync(cancellationToken);
            await _audit.LogAsync("PAYROLL_SUBMITTED", $"Payroll:{payroll.Id}", afterState: new { payroll.Id, payroll.Status, JobPaymentId = jobPayment.Id }, actorUserId: actorUserId.ToString(), cancellationToken: cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return Result.Success(jobPayment);
        }
        catch (DbUpdateException exception)
        {
            if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
            _logger.LogWarning(exception, "Payroll submission conflicted for payroll {PayrollId}.", payrollId);
            return Result.Failure<JobPayment>("Payroll submission conflicted; refresh and retry.");
        }
    }

    private Payroll CreatePayroll(SalaryDefinition definition, DateOnly dueDate)
    {
        var entries = new List<PayrollEntry>
        {
            new() { Description = definition.Description, Amount = definition.BaseAmount, Type = PayrollEntryType.Base, IsLocked = true, SortOrder = 0, CreatedAtUtc = _clock.UtcNow }
        };

        AddAdjustmentEntries(entries, definition, SalaryAdjustmentType.Benefit, PayrollEntryType.Benefit, 1);
        AddAdjustmentEntries(entries, definition, SalaryAdjustmentType.Deduction, PayrollEntryType.Deduction, -1);

        return new Payroll
        {
            SalaryDefinitionId = definition.Id,
            UserId = definition.UserId,
            PeriodEndingDate = dueDate,
            Description = definition.Description,
            PayrollTotal = entries.Sum(entry => entry.Amount),
            Status = PayrollStatus.Generated,
            IsLocked = false,
            GeneratedAtUtc = _clock.UtcNow,
            Entries = entries
        };
    }

    private static void AddAdjustmentEntries(List<PayrollEntry> entries, SalaryDefinition definition, SalaryAdjustmentType adjustmentType, PayrollEntryType entryType, int sign)
    {
        foreach (var adjustment in definition.Adjustments.Where(a => a.Type == adjustmentType).OrderBy(a => a.Id).ThenBy(a => a.Title, StringComparer.Ordinal))
        {
            var amount = (definition.BaseAmount * adjustment.PercentageRate) + adjustment.FixedValue;
            entries.Add(new PayrollEntry { Description = adjustment.Title, Amount = sign * amount, Type = entryType, IsLocked = true, SortOrder = entries.Count });
        }
    }

    private async Task<bool> IsAccountantAsync(Guid actorUserId, CancellationToken cancellationToken) =>
        await _db.Users.Where(user => user.Id == actorUserId && user.IsActive)
            .Select(user => (UserRole?)user.Role)
            .SingleOrDefaultAsync(cancellationToken) is { } role && role.HasMinimumRole(UserRole.Accountant);
}
