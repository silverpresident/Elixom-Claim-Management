using ElixomClaim.Lib.Common;
using ElixomClaim.Lib.Data;
using ElixomClaim.Lib.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ElixomClaim.Lib.Services;

public interface ISalaryPayrollService
{
    Task<Result<Payroll>> GenerateForDefinitionAsync(long salaryDefinitionId, Guid actorUserId, DateOnly asOfDate, CancellationToken cancellationToken = default);
}

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
