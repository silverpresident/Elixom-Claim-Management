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

public class ManagerJobPaymentsWorkflowTests
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
    public async Task Create_Get_PreselectsClaimantFromAcceptedClaimWorkflow()
    {
        var db = CreateInMemoryDbContext();
        var managerId = Guid.NewGuid();
        var claimantId = Guid.NewGuid();
        db.Users.AddRange(
            new User { Id = managerId, Email = "mgr@elixom.com", Role = UserRole.Manager, IsActive = true },
            new User { Id = claimantId, Email = "claimant@elixom.com", FullName = "Claimant", Role = UserRole.User, IsActive = true });
        await db.SaveChangesAsync();

        var controller = new JobPaymentsController(
            db,
            new JobPaymentService(db, new AuditService(db, NullLogger<AuditService>.Instance), new SystemClock(), NullLogger<JobPaymentService>.Instance));

        var result = await controller.Create(claimantId, null);

        Assert.IsType<ViewResult>(result);
        Assert.Equal(claimantId, controller.ViewData["SelectedPayeeUserId"]);
        Assert.Contains(((IEnumerable<User>)controller.ViewData["Users"]!).ToList(), user => user.Id == claimantId);
    }

    [Fact]
    public async Task Manager_CreateEditDeductionAttachAndSubmit_JobPaymentWorkflow()
    {
        var db = CreateInMemoryDbContext();
        var managerId = Guid.NewGuid();
        var payeeUserId = Guid.NewGuid();
        var clock = new SystemClock();

        var manager = new User { Id = managerId, Email = "mgr@elixom.com", Role = UserRole.Manager, IsActive = true };
        var payee = new User { Id = payeeUserId, Email = "payee@elixom.com", FullName = "Payee User", Role = UserRole.User, IsActive = true, BankName = "First Bank", BankAccountNumber = "11223344" };
        db.Users.AddRange(manager, payee);
        await db.SaveChangesAsync();

        var claimService = new ClaimService(db, new AuditService(db, NullLogger<AuditService>.Instance), NullLogger<ClaimService>.Instance);
        var claim = await claimService.CreateDraftAsync(new CreateClaimCommand(payeeUserId, "Claim 1", "Taxi", 500m));
        claim.Status = ClaimStatus.Accepted;
        await db.SaveChangesAsync();

        var jobService = new JobPaymentService(db, new AuditService(db, NullLogger<AuditService>.Instance), clock, NullLogger<JobPaymentService>.Instance);
        var controller = new JobPaymentsController(db, jobService)
        {
            TempData = new TempDataDictionary(new DefaultHttpContext(), new TestTempDataProvider())
        };

        var claimsUser = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new SecurityClaim(ClaimTypes.NameIdentifier, managerId.ToString()),
            new SecurityClaim(ClaimTypes.Role, UserRole.Manager.ToString())
        }, "TestAuth"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsUser }
        };

        // 1. Create Job Payment
        var createResult = await controller.Create(payeeUserId, null, "Manager Job 1", "Public Note", "Internal Note");
        var redirect = Assert.IsType<RedirectToActionResult>(createResult);
        var jobId = (Guid)redirect.RouteValues!["id"]!;

        // 2. Attach Claim
        var attachResult = await controller.AttachClaim(jobId, claim.Id);
        Assert.IsType<RedirectToActionResult>(attachResult);

        // 3. Add Deduction
        var deductResult = await controller.AddDeduction(jobId, "Processing Fee Deduction", 50m);
        Assert.IsType<RedirectToActionResult>(deductResult);

        // 4. Edit Metadata
        var editResult = await controller.Edit(jobId, "Updated Manager Job 1", "Updated Public Note", "Updated Internal Note");
        Assert.IsType<RedirectToActionResult>(editResult);

        // 5. Submit
        var submitResult = await controller.Submit(jobId);
        Assert.IsType<RedirectToActionResult>(submitResult);

        var job = await db.JobPayments.Include(j => j.Claims).Include(j => j.Deductions).FirstOrDefaultAsync(j => j.Id == jobId);
        Assert.NotNull(job);
        Assert.Equal(JobPaymentStatus.Submitted, job.Status);
        Assert.Equal("Updated Manager Job 1", job.Title);
        Assert.Equal(500m, job.JobTotal);
        Assert.Equal(50m, job.TotalDeductions);
        Assert.Equal(450m, job.TotalPaid);
        Assert.Single(job.Claims);
        Assert.Single(job.Deductions);
    }
}
