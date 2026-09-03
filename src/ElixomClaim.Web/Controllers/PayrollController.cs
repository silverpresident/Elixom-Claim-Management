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
    public async Task<IActionResult> Index() => View(new PayrollWorkspaceViewModel { SalaryDefinitions = await _db.SalaryDefinitions.AsNoTracking().Include(definition => definition.User).Include(definition => definition.Adjustments).OrderBy(definition => definition.User.FullName).ToListAsync(), Payrolls = await _db.Payrolls.AsNoTracking().Include(payroll => payroll.User).Include(payroll => payroll.Entries).OrderByDescending(payroll => payroll.GeneratedAtUtc).Take(50).ToListAsync(), AuditRecords = await _db.AuditRecords.AsNoTracking().Where(record => record.Target.StartsWith("Payroll:") || record.Target.StartsWith("SalaryDefinition:")).OrderByDescending(record => record.TimestampUtc).Take(20).ToListAsync() });
    [HttpGet("salary-definitions/create")] public async Task<IActionResult> Create() { ViewBag.Users = await _db.Users.AsNoTracking().Where(user => user.IsActive).OrderBy(user => user.FullName).ToListAsync(); return View(); }
    [HttpPost("salary-definitions/create")][ValidateAntiForgeryToken] public async Task<IActionResult> Create(CreateSalaryDefinitionInput input) { var result = await _service.CreateDefinitionAsync(new(ActorId(), input.UserId, input.Description, input.BaseAmount, input.FirstSalaryDate, input.StartDate, input.EndDate, input.RecurrenceDays, input.RecurrenceMonths, input.NearestWeekday)); if (result.IsSuccess) return RedirectToAction(nameof(Index)); ModelState.AddModelError(string.Empty, result.Error); ViewBag.Users = await _db.Users.AsNoTracking().Where(user => user.IsActive).OrderBy(user => user.FullName).ToListAsync(); return View(input); }
    [HttpPost("salary-definitions/{id:long}/generate")][ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateNow(long id)
    {
        var actor = ActorId();
        var result = await _service.GenerateForDefinitionAsync(id, actor, DateOnly.FromDateTime(DateTime.UtcNow));
        TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] = result.IsSuccess ? "Payroll generated." : result.Error;
        return RedirectToAction(nameof(Index));
    }
    [HttpPost("{id:long}/submit")][ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(long id)
    {
        var actor = ActorId();
        var result = await _service.SubmitAsync(id, actor);
        TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] = result.IsSuccess ? "Payroll submitted to a processing job payment." : result.Error;
        return RedirectToAction(nameof(Index));
    }
    private Guid ActorId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
public sealed class CreateSalaryDefinitionInput { public Guid UserId { get; set; } public string Description { get; set; } = string.Empty; public decimal BaseAmount { get; set; } public DateOnly FirstSalaryDate { get; set; } public DateOnly StartDate { get; set; } public DateOnly? EndDate { get; set; } public int RecurrenceDays { get; set; } public int RecurrenceMonths { get; set; } public DayOfWeek NearestWeekday { get; set; } }
