using System.Security.Claims;
using ElixomClaim.Lib.Data;
using ElixomClaim.Lib.Entities;
using ElixomClaim.Lib.Services;
using ElixomClaim.Web.Controllers;
using ElixomClaim.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using SecurityClaim = System.Security.Claims.Claim;

namespace ElixomClaim.Web.Tests.Controllers;

public class ClaimsControllerTests
{
    private static ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task Index_ReturnsClaimsAndPaymentHistoryForUser()
    {
        var db = CreateInMemoryDbContext();
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        var claimService = new ClaimService(db, new AuditService(db, NullLogger<AuditService>.Instance), NullLogger<ClaimService>.Instance);

        // Seed claims
        var claim1 = await claimService.CreateDraftAsync(new CreateClaimCommand(userId, "Claim 1", "Desc 1", 100m));
        var claim2 = await claimService.CreateDraftAsync(new CreateClaimCommand(otherUserId, "Other Claim", "Desc 2", 200m));

        // Seed job payments
        var job1 = new JobPayment
        {
            Id = Guid.NewGuid(),
            PayeeUserId = userId,
            Title = "Payout for Claim 1",
            Status = JobPaymentStatus.Paid,
            TotalPaid = 100m,
            PaymentDateUtc = DateTime.UtcNow,
            PaymentTransactionNumber = "TXN12345"
        };
        var job2 = new JobPayment
        {
            Id = Guid.NewGuid(),
            PayeeUserId = otherUserId,
            Title = "Payout for Other Claim",
            Status = JobPaymentStatus.Paid,
            TotalPaid = 200m,
            PaymentDateUtc = DateTime.UtcNow
        };
        db.JobPayments.AddRange(job1, job2);
        await db.SaveChangesAsync();

        var controller = new ClaimsController(claimService, db);
        var claimsUser = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new SecurityClaim(ClaimTypes.NameIdentifier, userId.ToString()),
            new SecurityClaim(ClaimTypes.Role, UserRole.User.ToString())
        }, "TestAuth"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsUser }
        };

        var result = await controller.Index();
        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<UserDashboardViewModel>(viewResult.Model);

        var userClaim = Assert.Single(model.Claims);
        Assert.Equal(claim1.Id, userClaim.Id);

        var userPayment = Assert.Single(model.PaymentHistory);
        Assert.Equal(job1.Id, userPayment.Id);
        Assert.Equal("TXN12345", userPayment.PaymentTransactionNumber);
    }

    [Fact]
    public async Task PaymentHistory_ReturnsOnlyTheCurrentUsersPayments_AndDashboardIsBounded()
    {
        var db = CreateInMemoryDbContext();
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var claimService = new ClaimService(db, new AuditService(db, NullLogger<AuditService>.Instance), NullLogger<ClaimService>.Instance);
        for (var i = 0; i < 6; i++)
            db.JobPayments.Add(new JobPayment { Id = Guid.NewGuid(), PayeeUserId = userId, Title = $"Payment {i}", TotalPaid = i + 1, CreatedAtUtc = DateTime.UtcNow.AddDays(-i) });
        db.JobPayments.Add(new JobPayment { Id = Guid.NewGuid(), PayeeUserId = otherUserId, Title = "Other payment", TotalPaid = 100m, CreatedAtUtc = DateTime.UtcNow.AddDays(1) });
        await db.SaveChangesAsync();
        var controller = new ClaimsController(claimService, db)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new SecurityClaim(ClaimTypes.NameIdentifier, userId.ToString()) }, "TestAuth"))
                }
            }
        };

        var dashboard = Assert.IsType<ViewResult>(await controller.Index());
        Assert.Equal(5, Assert.IsType<UserDashboardViewModel>(dashboard.Model).PaymentHistory.Count());
        var history = Assert.IsType<ViewResult>(await controller.PaymentHistory());
        var payments = Assert.IsAssignableFrom<IEnumerable<JobPayment>>(history.Model);
        Assert.Equal(6, payments.Count());
        Assert.DoesNotContain(payments, payment => payment.PayeeUserId == otherUserId);
    }
}
