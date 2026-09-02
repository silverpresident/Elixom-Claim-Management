using ElixomClaim.Lib.Data;
using ElixomClaim.Lib.Entities;
using ElixomClaim.Web.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ElixomClaim.Web.Tests.Controllers;

public class JobPaymentsControllerTests
{
    [Fact]
    public async Task AccountantQueue_ReturnsOnlySubmittedAndScheduledPayments()
    {
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var payee = new User { Email = "payee@anonymized.example.com", NormalizedEmail = "PAYEE@ANONYMIZED.EXAMPLE.COM", FullName = "Payee" };
        db.Users.Add(payee);
        db.JobPayments.AddRange(
            new JobPayment { PayeeUserId = payee.Id, Status = JobPaymentStatus.Processing },
            new JobPayment { PayeeUserId = payee.Id, Status = JobPaymentStatus.Submitted },
            new JobPayment { PayeeUserId = payee.Id, Status = JobPaymentStatus.Scheduled },
            new JobPayment { PayeeUserId = payee.Id, Status = JobPaymentStatus.Paid });
        await db.SaveChangesAsync();
        var controller = new JobPaymentsController(db, null!);

        var result = await controller.AccountantQueue();

        var view = Assert.IsType<ViewResult>(result);
        var jobs = Assert.IsAssignableFrom<IEnumerable<JobPayment>>(view.Model);
        Assert.Equal(new[] { JobPaymentStatus.Submitted, JobPaymentStatus.Scheduled }, jobs.Select(j => j.Status).Order());
    }
}
