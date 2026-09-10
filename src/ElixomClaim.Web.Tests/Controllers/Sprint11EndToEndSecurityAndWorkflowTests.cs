using System.Security.Claims;
using ElixomClaim.Lib.Data;
using ElixomClaim.Lib.Entities;
using ElixomClaim.Lib.Services;
using ElixomClaim.Web.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using SecurityClaim = System.Security.Claims.Claim;
using DomainClaim = ElixomClaim.Lib.Entities.Claim;

namespace ElixomClaim.Web.Tests.Controllers;

public class Sprint11EndToEndSecurityAndWorkflowTests
{
    private class TestTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    private static ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static ControllerContext CreateControllerContext(User user, HttpContext? httpContext = null)
    {
        var ctx = httpContext ?? new DefaultHttpContext();
        var claimsUser = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new SecurityClaim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new SecurityClaim(ClaimTypes.Email, user.Email),
            new SecurityClaim(ClaimTypes.Role, user.Role.ToString())
        }, "TestAuth"));

        ctx.User = claimsUser;
        return new ControllerContext { HttpContext = ctx };
    }

    [Fact]
    public async Task ProfileWorkflow_UserBankDetails_RedactsInNonPrivilegedProjections()
    {
        var db = CreateInMemoryDbContext();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "testuser@elixom.com",
            FullName = "Test User",
            Role = UserRole.User,
            BankAccountNumber = "1234567890",
            BankBranchCode = "001",
            IsActive = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var auditService = new AuditService(db, NullLogger<AuditService>.Instance);
        var httpContext = new DefaultHttpContext();
        var controller = new ProfileController(db, auditService, NullLogger<ProfileController>.Instance)
        {
            ControllerContext = CreateControllerContext(user, httpContext),
            TempData = new TempDataDictionary(httpContext, new TestTempDataProvider())
        };

        // 1. View profile
        var viewResult = Assert.IsType<ViewResult>(await controller.Index());
        var userModel = Assert.IsType<User>(viewResult.Model);
        Assert.Equal("1234567890", userModel.BankAccountNumber);

        var summary = Assert.IsType<UserProfileSummary>(controller.ViewBag.Summary);
        Assert.Equal("******7890", summary.BankAccountNumber);

        // 2. Update bank details
        var updateInput = new ProfileController.UpdateBankDetailsInput(
            BankAccountName: "Test Account",
            BankAccountNumber: "0987654321",
            BankName: "Test Bank",
            BankBranchCode: "002",
            BankBranchName: "New Kingston",
            BankAccountType: "Current"
        );
        var redirectResult = Assert.IsType<RedirectToActionResult>(await controller.UpdateBankDetails(updateInput));
        Assert.Equal("Index", redirectResult.ActionName);

        // Verify database updated
        var updatedUser = await db.Users.FindAsync(user.Id);
        Assert.NotNull(updatedUser);
        Assert.Equal("0987654321", updatedUser.BankAccountNumber);

        // Verify audit log redacted account number in AfterStateJson
        var audit = await db.AuditRecords.FirstOrDefaultAsync(a => a.Action == "USER_BANK_DETAILS_UPDATED");
        Assert.NotNull(audit);
        Assert.DoesNotContain("0987654321", audit.AfterStateJson ?? "");
    }

    [Fact]
    public async Task AdminCollectionClientsWorkflow_NotesAndFeesAccessControl()
    {
        var db = CreateInMemoryDbContext();
        var clock = new SystemClock();
        var admin = new User { Id = Guid.NewGuid(), Email = "admin@elixom.com", FullName = "Admin User", Role = UserRole.Administrator, IsActive = true };
        db.Users.Add(admin);
        await db.SaveChangesAsync();

        var auditService = new AuditService(db, NullLogger<AuditService>.Instance);
        var adminService = new CollectionClientAdministrationService(db, auditService, clock, NullLogger<CollectionClientAdministrationService>.Instance);

        var createCommand = new CreateCollectionClientCommand(
            ActorUserId: admin.Id,
            Name: "Client Alpha",
            Description: "Alpha Description",
            Notes: "Secret Internal Notes",
            PerJobProcessingFee: 10.00m,
            PerTransactionFee: 2.00m
        );
        var result = await adminService.CreateClientAsync(createCommand);
        Assert.True(result.IsSuccess);
        var client = result.Value;
        Assert.NotNull(client);
        Assert.Equal("Secret Internal Notes", client.Notes);

        // Non-admin summary projection should omit internal notes
        var summary = await db.CollectionClients
            .Select(c => new { c.Id, c.Name, c.Description, c.PerJobProcessingFee })
            .FirstOrDefaultAsync(c => c.Id == client.Id);

        Assert.NotNull(summary);
        Assert.Equal("Client Alpha", summary.Name);
    }

    [Fact]
    public async Task JobPaymentsWorkflow_EndToEnd_LifecycleAndSettlementCascade()
    {
        var db = CreateInMemoryDbContext();
        var clock = new SystemClock();
        var auditService = new AuditService(db, NullLogger<AuditService>.Instance);
        var jobService = new JobPaymentService(db, auditService, clock, NullLogger<JobPaymentService>.Instance);

        var manager = new User { Id = Guid.NewGuid(), Email = "manager@elixom.com", Role = UserRole.Manager, IsActive = true };
        var accountant = new User { Id = Guid.NewGuid(), Email = "accountant@elixom.com", Role = UserRole.Accountant, IsActive = true };
        var claimant = new User { Id = Guid.NewGuid(), Email = "claimant@elixom.com", Role = UserRole.User, IsActive = true, BankAccountNumber = "5555444433", BankName = "Test Bank", BankBranchCode = "001" };
        db.Users.AddRange(manager, accountant, claimant);

        var claim = new DomainClaim
        {
            Id = Guid.NewGuid(),
            ClaimantUserId = claimant.Id,
            Title = "Travel expenses",
            Description = "Mileage for field visit",
            Amount = 5000.00m,
            Status = ClaimStatus.Accepted,
            PaymentStatus = ClaimPaymentStatus.Unpaid,
            DateOfJob = DateTime.UtcNow
        };
        db.Claims.Add(claim);
        await db.SaveChangesAsync();

        // 1. Manager creates JobPayment for user claimant
        var createResult = await jobService.CreateAsync(new CreateJobPaymentCommand(
            ActorUserId: manager.Id,
            PayeeUserId: claimant.Id,
            CollectionClientId: null,
            Title: "Job Title",
            PublicNote: "Field visit payment",
            InternalNote: "Internal Note"
        ));
        Assert.True(createResult.IsSuccess);
        var job = createResult.Value;
        Assert.NotNull(job);
        Assert.Equal(JobPaymentStatus.Processing, job.Status);

        // 2. Attach claim
        var attachResult = await jobService.AttachClaimAsync(new AttachJobPaymentClaimCommand(
            ActorUserId: manager.Id,
            JobPaymentId: job.Id,
            ClaimId: claim.Id
        ));
        Assert.True(attachResult.IsSuccess);

        var updatedJob = await db.JobPayments.FindAsync(job.Id);
        Assert.NotNull(updatedJob);
        Assert.Equal(5000.00m, updatedJob.JobTotal);
        Assert.Equal(5000.00m, updatedJob.TotalPaid);

        // 3. Add deduction
        var deductResult = await jobService.AddDeductionAsync(new AddJobPaymentDeductionCommand(
            ActorUserId: manager.Id,
            JobPaymentId: job.Id,
            Description: "Advance recovery",
            Amount: 500.00m
        ));
        Assert.True(deductResult.IsSuccess);

        updatedJob = await db.JobPayments.FindAsync(job.Id);
        Assert.NotNull(updatedJob);
        Assert.Equal(5000.00m, updatedJob.JobTotal);
        Assert.Equal(500.00m, updatedJob.TotalDeductions);
        Assert.Equal(4500.00m, updatedJob.TotalPaid);

        // 4. Submit job
        var submitResult = await jobService.SubmitAsync(job.Id, manager.Id);
        Assert.True(submitResult.IsSuccess);

        // 5. Accountant schedules job
        var scheduleTime = DateTime.UtcNow.AddDays(1);
        var scheduleResult = await jobService.ScheduleAsync(job.Id, accountant.Id, scheduleTime);
        Assert.True(scheduleResult.IsSuccess);

        // 6. Accountant marks job paid (Settlement)
        var markPaidResult = await jobService.MarkPaidAsync(job.Id, accountant.Id, DateTime.UtcNow, "TXN-99001");
        Assert.True(markPaidResult.IsSuccess);

        var paidJob = await db.JobPayments.FindAsync(job.Id);
        Assert.NotNull(paidJob);
        Assert.Equal(JobPaymentStatus.Paid, paidJob.Status);
        Assert.Equal("5555444433", paidJob.PayoutBankAccountNumber);

        // Verify claim status updated to Paid
        var updatedClaim = await db.Claims.FindAsync(claim.Id);
        Assert.NotNull(updatedClaim);
        Assert.Equal(ClaimPaymentStatus.Paid, updatedClaim.PaymentStatus);

        // Verify notification outbox item created
        var outboxItem = await db.EmailOutboxItems.FirstOrDefaultAsync(o => o.RelatedEntityId == paidJob.Id.ToString());
        Assert.NotNull(outboxItem);
        Assert.Equal(claimant.Email, outboxItem.To);
    }

    [Fact]
    public async Task PayrollCustomEntries_NonNegativeNetPay_Enforced()
    {
        var db = CreateInMemoryDbContext();
        var clock = new SystemClock();
        var auditService = new AuditService(db, NullLogger<AuditService>.Instance);
        var planner = new SalaryRecurrencePlanner();
        var payrollService = new SalaryPayrollService(db, planner, auditService, clock, NullLogger<SalaryPayrollService>.Instance);

        var accountant = new User { Id = Guid.NewGuid(), Email = "accountant@elixom.com", Role = UserRole.Accountant, IsActive = true };
        var employee = new User { Id = Guid.NewGuid(), Email = "employee@elixom.com", Role = UserRole.User, IsActive = true };
        db.Users.AddRange(accountant, employee);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var salaryDef = new SalaryDefinition
        {
            Id = Guid.NewGuid(),
            UserId = employee.Id,
            Description = "Monthly salary",
            BaseAmount = 10000.00m,
            FirstSalaryDate = today.AddMonths(-1),
            LastSalaryDate = today.AddMonths(-1),
            StartDate = today.AddMonths(-3),
            RecurrenceMonths = 1,
            NearestWeekday = today.DayOfWeek
        };
        db.SalaryDefinitions.Add(salaryDef);
        await db.SaveChangesAsync();

        var generateResult = await payrollService.GenerateForDefinitionAsync(salaryDef.Id, accountant.Id, today);
        Assert.True(generateResult.IsSuccess);
        var payroll = generateResult.Value;
        Assert.NotNull(payroll);
        Assert.Equal(10000.00m, payroll.PayrollTotal);

        // Attempting to add custom negative deduction exceeding payroll total should fail
        var customResult = await payrollService.AddCustomEntryAsync(payroll.Id, accountant.Id, "Excessive deduction", -15000.00m);
        Assert.True(customResult.IsFailure);
        Assert.Contains("negative", customResult.Error, StringComparison.OrdinalIgnoreCase);
    }
}
