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

    [Fact]
    public async Task Details_LoadsLinkedPayrollAndItsEntries()
    {
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var user = new User { Email = "payee@anonymized.example.com", NormalizedEmail = "PAYEE@ANONYMIZED.EXAMPLE.COM", FullName = "Payee" };
        var definition = new SalaryDefinition { User = user, Description = "Salary", BaseAmount = 100m, FirstSalaryDate = new DateOnly(2026, 9, 1), LastSalaryDate = new DateOnly(2026, 9, 1), StartDate = new DateOnly(2026, 9, 1) };
        var payroll = new Payroll { SalaryDefinition = definition, User = user, PeriodEndingDate = new DateOnly(2026, 9, 30), Description = "September salary", PayrollTotal = 120m, Status = PayrollStatus.Submitted, IsLocked = true };
        var job = new JobPayment { PayeeUser = user, Status = JobPaymentStatus.Processing, JobTotal = 120m, TotalPaid = 120m };
        db.AddRange(user, definition, payroll, job);
        await db.SaveChangesAsync();
        db.AddRange(new PayrollEntry { PayrollId = payroll.Id, Description = "Base salary", Amount = 100m, Type = PayrollEntryType.Base, SortOrder = 0, IsLocked = true }, new JobPaymentPayroll { JobPaymentId = job.Id, PayrollId = payroll.Id });
        await db.SaveChangesAsync();
        var controller = new JobPaymentsController(db, null!);

        var result = await controller.Details(job.Id);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<JobPayment>(view.Model);
        var linkedPayroll = Assert.Single(model.Payrolls);
        Assert.Equal(payroll.Id, linkedPayroll.PayrollId);
        Assert.Equal("Base salary", Assert.Single(linkedPayroll.Payroll.Entries).Description);
    }
}
