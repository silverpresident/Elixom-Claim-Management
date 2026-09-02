using ElixomClaim.Lib.Data;
using ElixomClaim.Lib.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ElixomClaim.Lib.Tests.Data;

public class JobPaymentModelTests
{
    [Fact]
    public void JobPayment_HasPayeeExclusivityAndExactMoneyFields()
    {
        using var db = CreateDb();
        var entity = db.Model.FindEntityType(typeof(JobPayment))!;
        var migrationPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "ElixomClaim.Lib", "Migrations"));
        Assert.Contains("CK_JobPayments_ExactlyOnePayee", File.ReadAllText(Directory.GetFiles(migrationPath, "*AddJobPaymentEntities.cs").Single()));
        foreach (var property in new[] { nameof(JobPayment.JobTotal), nameof(JobPayment.ClientProcessingFee), nameof(JobPayment.TotalTxnProcessingFee), nameof(JobPayment.TotalDeductions), nameof(JobPayment.TotalPaid) })
        {
            Assert.Equal(18, entity.FindProperty(property)!.GetPrecision());
            Assert.Equal(2, entity.FindProperty(property)!.GetScale());
        }
        Assert.True(entity.FindProperty(nameof(JobPayment.RowVersion))!.IsConcurrencyToken);
    }

    [Fact]
    public void JobPaymentLineItems_CannotBeAttachedToTwoPayments()
    {
        using var db = CreateDb();
        Assert.True(db.Model.FindEntityType(typeof(JobPaymentClaim))!.GetIndexes().Single(i => i.Properties.Single().Name == nameof(JobPaymentClaim.ClaimId)).IsUnique);
        Assert.True(db.Model.FindEntityType(typeof(JobPaymentCollection))!.GetIndexes().Single(i => i.Properties.Single().Name == nameof(JobPaymentCollection.CollectionTransactionId)).IsUnique);
        Assert.True(db.Model.FindEntityType(typeof(JobPaymentPayroll))!.GetIndexes().Single(i => i.Properties.Single().Name == nameof(JobPaymentPayroll.PayrollId)).IsUnique);
    }

    private static ApplicationDbContext CreateDb() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}
