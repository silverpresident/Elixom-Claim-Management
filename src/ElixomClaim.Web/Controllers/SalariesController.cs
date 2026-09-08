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
[Route("salaries")]
public sealed class SalariesController : Controller
{
    private readonly ISalaryPayrollService _service;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<SalariesController> _logger;

    public SalariesController(ISalaryPayrollService service, ApplicationDbContext db, ILogger<SalariesController> logger)
    {
        _service = service;
        _db = db;
        _logger = logger;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var definitions = await _db.SalaryDefinitions.AsNoTracking().Include(definition => definition.User).Include(definition => definition.Adjustments).OrderBy(definition => definition.User.FullName).ToListAsync();
        var previews = new Dictionary<Guid, SalaryPayrollPreview>();
        foreach (var definition in definitions)
        {
            var preview = await _service.PreviewAsync(definition.Id, ActorId(), DateOnly.FromDateTime(DateTime.UtcNow));
            if (preview.IsSuccess) previews[definition.Id] = preview.Value!;
        }
        return View(new SalaryDefinitionsViewModel { SalaryDefinitions = definitions, Previews = previews });
    }

    [HttpGet("create")]
    public async Task<IActionResult> Create()
    {
        await PopulateUsersAsync();
        return View("~/Views/Payroll/Create.cshtml");
    }

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateSalaryDefinitionInput input)
    {
        var result = await _service.CreateDefinitionAsync(new(ActorId(), input.UserId, input.Description, input.BaseAmount, input.FirstSalaryDate, input.StartDate, input.EndDate, input.RecurrenceDays, input.RecurrenceMonths, input.NearestWeekday));
        if (result.IsSuccess)
        {
            _logger.LogInformation("Salary definition created by {ActorId} for {UserId}", ActorId(), input.UserId);
            TempData["SuccessMessage"] = "Salary definition created.";
            return RedirectToAction(nameof(Index));
        }
        ModelState.AddModelError(string.Empty, result.Error);
        await PopulateUsersAsync();
        return View("~/Views/Payroll/Create.cshtml", input);
    }

    [HttpPost("{id:guid}/adjustments")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddAdjustment(Guid id, [FromForm] string title, [FromForm] decimal percentageRate, [FromForm] decimal fixedValue, [FromForm] SalaryAdjustmentType type)
    {
        var result = await _service.AddAdjustmentAsync(new AddSalaryAdjustmentCommand(ActorId(), id, title, percentageRate, fixedValue, type));
        TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] = result.IsSuccess ? "Salary adjustment added." : result.Error;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:guid}/generate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateNow(Guid id)
    {
        var result = await _service.GenerateForDefinitionAsync(id, ActorId(), DateOnly.FromDateTime(DateTime.UtcNow));
        TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] = result.IsSuccess ? "Payroll generated." : result.Error;
        return RedirectToAction("Index", "Payroll");
    }

    private async Task PopulateUsersAsync() => ViewBag.Users = await _db.Users.AsNoTracking().Where(user => user.IsActive).OrderBy(user => user.FullName).ToListAsync();
    private Guid ActorId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
