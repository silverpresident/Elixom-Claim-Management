using System.Security.Claims;
using ElixomClaim.Lib.Authorization;
using ElixomClaim.Lib.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace ElixomClaim.Web.Controllers;
[Authorize(Policy = PolicyNames.RequireAccountant)]
[Route("payroll")]
public sealed class PayrollController : Controller
{
    private readonly ISalaryPayrollService _service;
    public PayrollController(ISalaryPayrollService service) => _service = service;
    [HttpPost("salary-definitions/{id:long}/generate")][ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateNow(long id)
    {
        var actor = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _service.GenerateForDefinitionAsync(id, actor, DateOnly.FromDateTime(DateTime.UtcNow));
        TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] = result.IsSuccess ? "Payroll generated." : result.Error;
        return RedirectToAction("Index", "Home");
    }
}
