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

public class PayrollAdjustmentsWorkflowTests
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
    public async Task AddSalaryAdjustmentAndCustomPayrollEntry_Workflow()
    {
        var db = CreateInMemoryDbContext();
        var accountantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var clock = new SystemClock();

        var accountant = new User { Id = accountantId, Email = "acct@elixom.com", Role = UserRole.Accountant, IsActive = true };
        var employee = new User { Id = userId, Email = "emp@elixom.com", FullName = "Employee", Role = UserRole.User, IsActive = true };
        db.Users.AddRange(accountant, employee);
        await db.SaveChangesAsync();

        var planner = new SalaryRecurrencePlanner();
        var payrollService = new SalaryPayrollService(db, planner, new AuditService(db, NullLogger<AuditService>.Instance), clock, NullLogger<SalaryPayrollService>.Instance);

        // 1. Create Salary Definition
        var createDefCmd = new CreateSalaryDefinitionCommand(
            accountantId, userId, "Monthly Salary", 100000m, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1), null, 0, 1, DayOfWeek.Friday);
        var defResult = await payrollService.CreateDefinitionAsync(createDefCmd);
        var def = defResult.Value!;

        var controller = new SalariesController(payrollService, db, NullLogger<SalariesController>.Instance)
        {
            TempData = new TempDataDictionary(new DefaultHttpContext(), new TestTempDataProvider())
        };

        var claimsUser = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new SecurityClaim(ClaimTypes.NameIdentifier, accountantId.ToString()),
            new SecurityClaim(ClaimTypes.Role, UserRole.Accountant.ToString())
        }, "TestAuth"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsUser }
        };

        // 2. Add Salary Adjustment
        var adjResult = await controller.AddAdjustment(def.Id, "Health Insurance", 0.05m, 0m, SalaryAdjustmentType.Deduction);
        Assert.IsType<RedirectToActionResult>(adjResult);

        var updatedDef = await db.SalaryDefinitions.Include(s => s.Adjustments).FirstOrDefaultAsync(s => s.Id == def.Id);
        Assert.Single(updatedDef!.Adjustments);

        // 3. Generate Payroll
        var genResult = await payrollService.GenerateForDefinitionAsync(def.Id, accountantId, new DateOnly(2026, 1, 31));
        var payroll = genResult.Value!;
        Assert.Equal(95000m, payroll.PayrollTotal);

        // 4. Add Custom Payroll Entry
        var payrollController = new PayrollController(payrollService, db, NullLogger<PayrollController>.Instance)
        {
            TempData = new TempDataDictionary(new DefaultHttpContext(), new TestTempDataProvider()),
            ControllerContext = controller.ControllerContext
        };
        var customResult = await payrollController.AddCustomEntry(payroll.Id, "Bonus Payment", 5000m);
        Assert.IsType<RedirectToActionResult>(customResult);

        var updatedPayroll = await db.Payrolls.Include(p => p.Entries).FirstOrDefaultAsync(p => p.Id == payroll.Id);
        Assert.NotNull(updatedPayroll);
        Assert.Equal(100000m, updatedPayroll.PayrollTotal);
        Assert.Contains(updatedPayroll.Entries, e => e.Description == "Bonus Payment" && e.Amount == 5000m);
    }

    [Fact]
    public async Task Workspace_SeparatesOutstandingPayrollsFromPaidHistory()
    {
        var db = CreateInMemoryDbContext();
        var employee = new User { Id = Guid.NewGuid(), Email = "emp@elixom.com", FullName = "Employee", Role = UserRole.User, IsActive = true };
        db.Users.Add(employee);
        db.Payrolls.AddRange(
            new Payroll { Id = Guid.NewGuid(), UserId = employee.Id, User = employee, PeriodEndingDate = new DateOnly(2026, 8, 31), Status = PayrollStatus.Generated },
            new Payroll { Id = Guid.NewGuid(), UserId = employee.Id, User = employee, PeriodEndingDate = new DateOnly(2026, 7, 31), Status = PayrollStatus.Submitted },
            new Payroll { Id = Guid.NewGuid(), UserId = employee.Id, User = employee, PeriodEndingDate = new DateOnly(2026, 6, 30), Status = PayrollStatus.Paid });
        await db.SaveChangesAsync();

        var service = new SalaryPayrollService(db, new SalaryRecurrencePlanner(), new AuditService(db, NullLogger<AuditService>.Instance), new SystemClock(), NullLogger<SalaryPayrollService>.Instance);
        var controller = new PayrollController(service, db, NullLogger<PayrollController>.Instance);

        var current = Assert.IsType<ViewResult>(await controller.Index());
        var currentModel = Assert.IsType<ElixomClaim.Web.Models.PayrollWorkspaceViewModel>(current.Model);
        Assert.False(currentModel.IsHistory);
        Assert.Equal(2, currentModel.Payrolls.Count);
        Assert.DoesNotContain(currentModel.Payrolls, payroll => payroll.Status == PayrollStatus.Paid);

        var history = Assert.IsType<ViewResult>(await controller.History());
        var historyModel = Assert.IsType<ElixomClaim.Web.Models.PayrollWorkspaceViewModel>(history.Model);
        Assert.True(historyModel.IsHistory);
        Assert.Single(historyModel.Payrolls);
        Assert.Equal(PayrollStatus.Paid, historyModel.Payrolls[0].Status);
    }
}
