using ElixomClaim.Lib.Data;
using ElixomClaim.Lib.Entities;
using ElixomClaim.Lib.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ElixomClaim.Lib.Tests.Services;

public class SalaryPayrollServiceTests
{
    [Fact]
    public async Task GenerateForDefinitionAsync_CreatesLockedOrderedEntriesAndAdvancesCursor()
    {
        await using var db = CreateDb();
        var accountant = User(UserRole.Accountant);
        var payee = User(UserRole.User);
        var definition = new SalaryDefinition { UserId = payee.Id, Description = "Monthly salary", BaseAmount = 1000m, FirstSalaryDate = new(2025, 1, 1), LastSalaryDate = new(2025, 1, 31), StartDate = new(2025, 1, 1), RecurrenceMonths = 1, NearestWeekday = DayOfWeek.Friday };
        definition.Adjustments.Add(new SalaryAdjustment { Title = "Health benefit", Type = SalaryAdjustmentType.Benefit, PercentageRate = .1m });
        definition.Adjustments.Add(new SalaryAdjustment { Title = "Tax", Type = SalaryAdjustmentType.Deduction, FixedValue = 75m });
        db.AddRange(accountant, payee, definition); await db.SaveChangesAsync();

        var result = await Service(db).GenerateForDefinitionAsync(definition.Id, accountant.Id, new(2025, 2, 28));

        Assert.True(result.IsSuccess);
        var payroll = result.Value!;
        Assert.Equal(new DateOnly(2025, 2, 28), payroll.PeriodEndingDate);
        Assert.Equal(1025m, payroll.PayrollTotal);
        Assert.Equal(new[] { PayrollEntryType.Base, PayrollEntryType.Benefit, PayrollEntryType.Deduction }, payroll.Entries.OrderBy(entry => entry.SortOrder).Select(entry => entry.Type));
        Assert.All(payroll.Entries, entry => Assert.True(entry.IsLocked));
        Assert.Equal(payroll.PeriodEndingDate, definition.LastSalaryDate);
        Assert.Contains(db.AuditRecords, record => record.Action == "PAYROLL_GENERATED");
    }

    [Fact]
    public async Task GenerateForDefinitionAsync_RejectsUnauthorizedAndDuplicateDuePeriod()
    {
        await using var db = CreateDb();
        var user = User(UserRole.User);
        var accountant = User(UserRole.Accountant);
        var definition = new SalaryDefinition { UserId = user.Id, Description = "Salary", BaseAmount = 100m, FirstSalaryDate = new(2025, 1, 1), LastSalaryDate = new(2025, 1, 2), StartDate = new(2025, 1, 1), RecurrenceDays = 1, NearestWeekday = DayOfWeek.Friday };
        db.AddRange(user, accountant, definition); await db.SaveChangesAsync();
        var service = Service(db);

        Assert.True((await service.GenerateForDefinitionAsync(definition.Id, user.Id, new(2025, 1, 3))).IsFailure);
        Assert.True((await service.GenerateForDefinitionAsync(definition.Id, accountant.Id, new(2025, 1, 3))).IsSuccess);
        definition.LastSalaryDate = new(2025, 1, 2);
        Assert.True((await service.GenerateForDefinitionAsync(definition.Id, accountant.Id, new(2025, 1, 3))).IsFailure);
    }

    [Fact]
    public async Task CustomEntriesAndSubmission_KeepNetNonNegativeAndCreateProcessingJob()
    {
        await using var db = CreateDb();
        var accountant = User(UserRole.Accountant); var payee = User(UserRole.User);
        var definition = new SalaryDefinition { UserId = payee.Id, Description = "Salary", BaseAmount = 100m, FirstSalaryDate = new(2025, 1, 1), LastSalaryDate = new(2025, 1, 2), StartDate = new(2025, 1, 1), RecurrenceDays = 1, NearestWeekday = DayOfWeek.Friday };
        db.AddRange(accountant, payee, definition); await db.SaveChangesAsync();
        var service = Service(db);
        var payroll = (await service.GenerateForDefinitionAsync(definition.Id, accountant.Id, new(2025, 1, 3))).Value!;

        Assert.True((await service.AddCustomEntryAsync(payroll.Id, accountant.Id, "Recovery", -101m)).IsFailure);
        Assert.True((await service.AddCustomEntryAsync(payroll.Id, accountant.Id, "Allowance", 25m)).IsSuccess);
        var submitted = await service.SubmitAsync(payroll.Id, accountant.Id);

        Assert.True(submitted.IsSuccess);
        Assert.Equal(JobPaymentStatus.Processing, submitted.Value!.Status);
        Assert.Equal(125m, submitted.Value.JobTotal);
        Assert.Equal(PayrollStatus.Submitted, payroll.Status);
        Assert.True(payroll.IsLocked);
        Assert.All(payroll.Entries, entry => Assert.True(entry.IsLocked));
        Assert.Single(db.JobPaymentPayrolls);
        Assert.True((await service.AddCustomEntryAsync(payroll.Id, accountant.Id, "Late", 1m)).IsFailure);
    }

    private static SalaryPayrollService Service(ApplicationDbContext db) => new(db, new SalaryRecurrencePlanner(), new AuditService(db, NullLogger<AuditService>.Instance), new SystemClock(), NullLogger<SalaryPayrollService>.Instance);
    private static ApplicationDbContext CreateDb() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static User User(UserRole role) => new() { Email = $"{Guid.NewGuid():N}@anonymized.example.com", NormalizedEmail = Guid.NewGuid().ToString("N"), FullName = "Test User", Role = role };
}
