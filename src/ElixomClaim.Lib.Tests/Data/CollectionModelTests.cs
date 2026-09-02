using ElixomClaim.Lib.Data;
using ElixomClaim.Lib.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ElixomClaim.Lib.Tests.Data;

public class CollectionModelTests
{
    [Fact]
    public void CollectionTransaction_UsesJmdMoneyAndClientScopedOptionForeignKeys()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var context = new ApplicationDbContext(options);
        var transaction = context.Model.FindEntityType(typeof(CollectionTransaction));

        Assert.NotNull(transaction);
        Assert.Equal(18, transaction.FindProperty(nameof(CollectionTransaction.Amount))!.GetPrecision());
        Assert.Equal(2, transaction.FindProperty(nameof(CollectionTransaction.Amount))!.GetScale());
        Assert.Equal(18, transaction.FindProperty(nameof(CollectionTransaction.ProcessingFee))!.GetPrecision());
        Assert.Equal(2, transaction.FindProperty(nameof(CollectionTransaction.ProcessingFee))!.GetScale());
        Assert.Contains(transaction.GetForeignKeys(), foreignKey =>
            foreignKey.Properties.Select(property => property.Name)
                .SequenceEqual(new[] { nameof(CollectionTransaction.PurposeOptionId), nameof(CollectionTransaction.CollectionClientId) }));
        Assert.Contains(transaction.GetForeignKeys(), foreignKey =>
            foreignKey.Properties.Select(property => property.Name)
                .SequenceEqual(new[] { nameof(CollectionTransaction.AmountOptionId), nameof(CollectionTransaction.CollectionClientId) }));
    }

    [Fact]
    public void CollectionClientUser_HasCompositeAssignmentKey()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var context = new ApplicationDbContext(options);
        var assignment = context.Model.FindEntityType(typeof(CollectionClientUser));

        Assert.NotNull(assignment);
        Assert.Equal(
            new[] { nameof(CollectionClientUser.CollectionClientId), nameof(CollectionClientUser.UserId) },
            assignment.FindPrimaryKey()!.Properties.Select(property => property.Name));
    }

    [Theory]
    [InlineData(CollectionStatus.Collected)]
    [InlineData(CollectionStatus.Processing)]
    [InlineData(CollectionStatus.Transferred)]
    public void CollectionStatuses_AreExplicitLifecycleValues(CollectionStatus status)
    {
        Assert.True(Enum.IsDefined(status));
    }
}
