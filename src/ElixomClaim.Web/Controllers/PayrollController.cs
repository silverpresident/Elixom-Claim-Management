using System.Security.Claims;
using ElixomClaim.Lib.Authorization;
using ElixomClaim.Lib.Data;
using ElixomClaim.Lib.Entities;
using ElixomClaim.Lib.Services;
using ElixomClaim.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace ElixomClaim.Web.Controllers;

[Authorize(Policy = PolicyNames.RequireAccountant)]
[Route("payroll")]
public sealed class PayrollController : Controller
{
    private readonly ISalaryPayrollService _service; private readonly ApplicationDbContext _db; private readonly ILogger<PayrollController> _logger;
    public PayrollController(ISalaryPayrollService service, ApplicationDbContext db, ILogger<PayrollController> logger) { _service = service; _db = db; _logger = logger; }
    [HttpGet("")]
    public Task<IActionResult> Index() => WorkspaceAsync(isHistory: false);

    [HttpGet("history")]
    public Task<IActionResult> History() => WorkspaceAsync(isHistory: true);

    private async Task<IActionResult> WorkspaceAsync(bool isHistory)
    {
        IQueryable<Payroll> payrolls = _db.Payrolls.AsNoTracking().Include(payroll => payroll.User).Include(payroll => payroll.Entries);
        payrolls = isHistory
            ? payrolls.Where(payroll => payroll.Status == PayrollStatus.Paid)
            : payrolls.Where(payroll => payroll.Status == PayrollStatus.Generated || payroll.Status == PayrollStatus.Submitted);

        return View("Index", new PayrollWorkspaceViewModel
        {
            IsHistory = isHistory,
            Payrolls = await payrolls.OrderByDescending(payroll => payroll.GeneratedAtUtc).Take(50).ToListAsync(),
            AuditRecords = isHistory
                ? await _db.AuditRecords.AsNoTracking().Where(record => record.Target.StartsWith("Payroll:")).OrderByDescending(record => record.TimestampUtc).Take(20).ToListAsync()
                : []
        });
    }
    [HttpPost("{id:guid}/custom-entries")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddCustomEntry(Guid id, [FromForm] string description, [FromForm] decimal amount)
    {
        var result = await _service.AddCustomEntryAsync(id, ActorId(), description, amount);
        TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] = result.IsSuccess ? "Custom payroll entry added." : result.Error;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:guid}/submit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(Guid id)
    {
        var actor = ActorId();
        var result = await _service.SubmitAsync(id, actor);
        TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] = result.IsSuccess ? "Payroll submitted to a processing job payment." : result.Error;
        return RedirectToAction(nameof(Index));
    }
    private Guid ActorId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
public sealed class CreateSalaryDefinitionInput { public Guid UserId { get; set; } public string Description { get; set; } = string.Empty; public decimal BaseAmount { get; set; } public DateOnly FirstSalaryDate { get; set; } public DateOnly StartDate { get; set; } public DateOnly? EndDate { get; set; } public int RecurrenceDays { get; set; } public int RecurrenceMonths { get; set; } public DayOfWeek NearestWeekday { get; set; } }
