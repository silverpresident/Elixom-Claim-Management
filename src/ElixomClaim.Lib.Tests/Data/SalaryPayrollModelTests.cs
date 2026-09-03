using ElixomClaim.Lib.Data;
using ElixomClaim.Lib.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace ElixomClaim.Lib.Tests.Data;

public class SalaryPayrollModelTests
{
    [Fact]
    public void SalaryAndPayrollSchema_EnforcesRangesOrderingAndDuePeriodIdentity()
    {
        using var db = CreateDb();

        var model = db.GetService<IDesignTimeModel>().Model;
        var salary = model.FindEntityType(typeof(SalaryDefinition))!;
        var adjustment = model.FindEntityType(typeof(SalaryAdjustment))!;
        var payroll = model.FindEntityType(typeof(Payroll))!;
        var entry = model.FindEntityType(typeof(PayrollEntry))!;

        Assert.Contains("CK_SalaryDefinitions_BaseAmount", salary.GetCheckConstraints().Select(x => x.Name));
        Assert.Contains("CK_SalaryDefinitions_Recurrence", salary.GetCheckConstraints().Select(x => x.Name));
        Assert.Contains("CK_SalaryAdjustments_Range", adjustment.GetCheckConstraints().Select(x => x.Name));
        Assert.Equal(18, adjustment.FindProperty(nameof(SalaryAdjustment.PercentageRate))!.GetPrecision());
        Assert.Equal(3, adjustment.FindProperty(nameof(SalaryAdjustment.PercentageRate))!.GetScale());
        Assert.Equal(18, payroll.FindProperty(nameof(Payroll.PayrollTotal))!.GetPrecision());
        Assert.Equal(2, payroll.FindProperty(nameof(Payroll.PayrollTotal))!.GetScale());

        var duePeriodIndex = payroll.GetIndexes().Single(x => x.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(Payroll.SalaryDefinitionId), nameof(Payroll.PeriodEndingDate) }));
        Assert.True(duePeriodIndex.IsUnique);
        Assert.Null(duePeriodIndex.GetFilter());
        Assert.True(entry.GetIndexes().Single(x => x.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(PayrollEntry.PayrollId), nameof(PayrollEntry.SortOrder) })).IsUnique);
    }

    private static ApplicationDbContext CreateDb() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}
