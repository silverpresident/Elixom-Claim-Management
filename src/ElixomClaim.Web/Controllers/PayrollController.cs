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
    public async Task<IActionResult> Index() => View(new PayrollWorkspaceViewModel { SalaryDefinitions = await _db.SalaryDefinitions.AsNoTracking().Include(definition => definition.User).Include(definition => definition.Adjustments).OrderBy(definition => definition.User.FullName).ToListAsync(), Payrolls = await _db.Payrolls.AsNoTracking().Include(payroll => payroll.User).Include(payroll => payroll.Entries).OrderByDescending(payroll => payroll.GeneratedAtUtc).Take(50).ToListAsync() });
    [HttpPost("salary-definitions/{id:long}/generate")][ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateNow(long id)
    {
        var actor = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _service.GenerateForDefinitionAsync(id, actor, DateOnly.FromDateTime(DateTime.UtcNow));
        TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] = result.IsSuccess ? "Payroll generated." : result.Error;
        return RedirectToAction(nameof(Index));
    }
    [HttpPost("{id:long}/submit")][ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(long id)
    {
        var actor = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _service.SubmitAsync(id, actor);
        TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] = result.IsSuccess ? "Payroll submitted to a processing job payment." : result.Error;
        return RedirectToAction(nameof(Index));
    }
}
