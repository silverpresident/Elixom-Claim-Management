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

namespace ElixomClaim.Web.Tests.Controllers;

public class AccountantJobPaymentsWorkflowTests
{
    private static ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private class TestTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    [Fact]
    public async Task Accountant_ScheduleAndSettleJobPayment_AndAdjustmentFlow()
    {
        var db = CreateInMemoryDbContext();
        var accountantId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var payeeUserId = Guid.NewGuid();
        var clock = new SystemClock();

        var accountant = new User { Id = accountantId, Email = "acct@elixom.com", Role = UserRole.Accountant, IsActive = true };
        var admin = new User { Id = adminId, Email = "admin@elixom.com", Role = UserRole.Administrator, IsActive = true };
        var payee = new User { Id = payeeUserId, Email = "payee2@elixom.com", FullName = "Payee 2", Role = UserRole.User, IsActive = true, BankName = "Commercial Bank", BankAccountNumber = "99887766" };
        db.Users.AddRange(accountant, admin, payee);
        await db.SaveChangesAsync();

        var jobService = new JobPaymentService(db, new AuditService(db, NullLogger<AuditService>.Instance), clock, NullLogger<JobPaymentService>.Instance);

        // Seed Submitted Job Payment
        var createCmd = new CreateJobPaymentCommand(accountantId, payeeUserId, null, "Payout Note", "Internal Note", "Accountant Job");
        var createRes = await jobService.CreateAsync(createCmd);
        var job = createRes.Value!;
        job.JobTotal = 1000m;
        job.TotalPaid = 1000m;
        job.Status = JobPaymentStatus.Submitted;
        await db.SaveChangesAsync();

        var controller = new JobPaymentsController(db, jobService)
        {
            TempData = new TempDataDictionary(new DefaultHttpContext(), new TestTempDataProvider())
        };

        var acctUserPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new SecurityClaim(ClaimTypes.NameIdentifier, accountantId.ToString()),
            new SecurityClaim(ClaimTypes.Role, UserRole.Accountant.ToString())
        }, "TestAuth"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = acctUserPrincipal }
        };

        // 1. Schedule Payment
        var scheduledTime = DateTime.UtcNow.AddDays(1);
        var scheduleResult = await controller.Schedule(job.Id, scheduledTime);
        Assert.IsType<RedirectToActionResult>(scheduleResult);

        var scheduledJob = await db.JobPayments.FindAsync(job.Id);
        Assert.Equal(JobPaymentStatus.Scheduled, scheduledJob!.Status);

        // 2. Mark Paid (Settle)
        var paymentDate = DateTime.UtcNow;
        var markPaidResult = await controller.MarkPaid(job.Id, paymentDate, "TXN-8877");
        Assert.IsType<RedirectToActionResult>(markPaidResult);

        var paidJob = await db.JobPayments.FindAsync(job.Id);
        Assert.Equal(JobPaymentStatus.Paid, paidJob!.Status);
        Assert.Equal("TXN-8877", paidJob.PaymentTransactionNumber);

        // Check Outbox Email Created
        var outbox = await db.EmailOutboxItems.FirstOrDefaultAsync(e => e.RelatedEntityId == job.Id.ToString());
        Assert.NotNull(outbox);
        Assert.Equal("payee2@elixom.com", outbox.To);

        // 3. Create Adjustment
        var adjustResult = await controller.CreateAdjustment(job.Id, -100m, "Overpayment correction");
        var adjustRedirect = Assert.IsType<RedirectToActionResult>(adjustResult);
        var adjustJobId = (Guid)adjustRedirect.RouteValues!["id"]!;

        var adjustJob = await db.JobPayments.FindAsync(adjustJobId);
        Assert.NotNull(adjustJob);
        Assert.True(adjustJob.IsAdjustment);
        Assert.True(adjustJob.IsRecoveryReceivable);

        // 4. Admin Approve Adjustment
        var adminUserPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new SecurityClaim(ClaimTypes.NameIdentifier, adminId.ToString()),
            new SecurityClaim(ClaimTypes.Role, UserRole.Administrator.ToString())
        }, "TestAuth"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = adminUserPrincipal }
        };

        var approveResult = await controller.ApproveAdjustment(adjustJobId);
        Assert.IsType<RedirectToActionResult>(approveResult);

        var approvedJob = await db.JobPayments.FindAsync(adjustJobId);
        Assert.NotNull(approvedJob!.ApprovedAtUtc);
    }
}
