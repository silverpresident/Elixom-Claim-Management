using ElixomClaim.Lib.Data;
using ElixomClaim.Lib.Entities;
using ElixomClaim.Lib.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ElixomClaim.Lib.Tests.Data;

public class DomainDataCompletionRelationalTests
{
    private static ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task CollectionClient_And_BankDetails_PersistNewFieldsAndNotes()
    {
        var db = CreateInMemoryDbContext();
        var client = new CollectionClient
        {
            Id = Guid.NewGuid(),
            Name = "Acme Corp",
            Description = "Corporate Clearing Account",
            Notes = "Internal VIP client notes",
            PerJobProcessingFee = 150.00m,
            PerTransactionFee = 25.50m
        };
        db.CollectionClients.Add(client);

        var bankDetail = new CollectionClientBankDetail
        {
            Id = Guid.NewGuid(),
            CollectionClientId = client.Id,
            AccountName = "Acme Operating",
            BankName = "First National Bank",
            BranchCode = "001",
            BranchName = "New Kingston",
            AccountType = CollectionBankAccountTypes.Current,
            AccountNumber = "9988776655",
            Notes = "Internal wire instructions note"
        };
        db.CollectionClientBankDetails.Add(bankDetail);
        await db.SaveChangesAsync();

        var fetchedClient = await db.CollectionClients.FindAsync(client.Id);
        Assert.NotNull(fetchedClient);
        Assert.Equal("Corporate Clearing Account", fetchedClient.Description);
        Assert.Equal("Internal VIP client notes", fetchedClient.Notes);
        Assert.Equal(150.00m, fetchedClient.PerJobProcessingFee);
        Assert.Equal(25.50m, fetchedClient.PerTransactionFee);

        var fetchedBank = await db.CollectionClientBankDetails.FindAsync(bankDetail.Id);
        Assert.NotNull(fetchedBank);
        Assert.Equal("Internal wire instructions note", fetchedBank.Notes);
        Assert.Equal("New Kingston", fetchedBank.BranchName);
        Assert.Equal(CollectionBankAccountTypes.Current, fetchedBank.AccountType);
    }

    [Fact]
    public async Task CollectionTransaction_PersistsPayorTelephone()
    {
        var db = CreateInMemoryDbContext();
        var client = new CollectionClient { Id = Guid.NewGuid(), Name = "Client A" };
        var purpose = new CollectionPurposeOption { Id = Guid.NewGuid(), CollectionClientId = client.Id, Name = "Dues" };
        var amount = new CollectionAmountOption { Id = Guid.NewGuid(), CollectionClientId = client.Id, Name = "Fee", Amount = 1000m };
        var teller = new User { Id = Guid.NewGuid(), Email = "teller@test.com", FullName = "Teller" };
        db.AddRange(client, purpose, amount, teller);
        await db.SaveChangesAsync();

        var txn = new CollectionTransaction
        {
            Id = Guid.NewGuid(),
            CollectionClientId = client.Id,
            PurposeOptionId = purpose.Id,
            AmountOptionId = amount.Id,
            TellerUserId = teller.Id,
            PayorName = "Payor Smith",
            PayorEmail = "smith@example.com",
            PayorTelephone = "876-555-0199",
            Method = CollectionMethod.Cash,
            Amount = 1000m,
            ProcessingFee = 20m,
            PaymentDateUtc = DateTime.UtcNow
        };
        db.CollectionTransactions.Add(txn);
        await db.SaveChangesAsync();

        var fetchedTxn = await db.CollectionTransactions.FindAsync(txn.Id);
        Assert.NotNull(fetchedTxn);
        Assert.Equal("876-555-0199", fetchedTxn.PayorTelephone);
    }

    [Fact]
    public async Task JobPaymentService_SnapshotsPayoutBankDetailsAtCreationTime()
    {
        var db = CreateInMemoryDbContext();
        var audit = new AuditService(db, NullLogger<AuditService>.Instance);
        var clock = new SystemClock();
        var jobService = new JobPaymentService(db, audit, clock, NullLogger<JobPaymentService>.Instance);

        var manager = new User { Id = Guid.NewGuid(), Email = "mgr@elixom.com", Role = UserRole.Manager };
        var payeeUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "payee@elixom.com",
            FullName = "Payee Person",
            Role = UserRole.User,
            BankAccountName = "Payee Person",
            BankAccountNumber = "1234567890",
            BankName = "National Commercial Bank",
            BankBranchCode = "007"
        };
        db.Users.AddRange(manager, payeeUser);
        await db.SaveChangesAsync();

        var createCmd = new CreateJobPaymentCommand(
            ActorUserId: manager.Id,
            PayeeUserId: payeeUser.Id,
            CollectionClientId: null,
            PublicNote: "Public description note",
            InternalNote: "Internal processing note",
            Title: "Staff Reimbursement Job"
        );

        var result = await jobService.CreateAsync(createCmd);
        Assert.True(result.IsSuccess);
        var job = result.Value!;

        Assert.Equal("Staff Reimbursement Job", job.Title);
        Assert.Equal("Public description note", job.PublicDescription);
        Assert.Equal("National Commercial Bank", job.PayoutBankName);
        Assert.Equal("Payee Person", job.PayoutBankAccountName);
        Assert.Equal("1234567890", job.PayoutBankAccountNumber);
        Assert.Equal("007", job.PayoutBankBranchCode);

        // Mutate user bank details to ensure snapshot is preserved on JobPayment
        payeeUser.BankName = "New Bank";
        payeeUser.BankAccountNumber = "9999999999";
        await db.SaveChangesAsync();

        var reFetchedJob = await db.JobPayments.FindAsync(job.Id);
        Assert.NotNull(reFetchedJob);
        Assert.Equal("National Commercial Bank", reFetchedJob.PayoutBankName);
        Assert.Equal("1234567890", reFetchedJob.PayoutBankAccountNumber);
    }
}
