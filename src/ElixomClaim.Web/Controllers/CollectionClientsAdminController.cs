using System.Security.Claims;
using ElixomClaim.Lib.Authorization;
using ElixomClaim.Lib.Data;
using ElixomClaim.Lib.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ElixomClaim.Web.Controllers;

[Authorize(Policy = PolicyNames.RequireAdministrator)]
[Route("admin/collection-clients")]
public class CollectionClientsAdminController : Controller
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ICollectionClientAdministrationService _service;
    private readonly ILogger<CollectionClientsAdminController> _logger;

    public CollectionClientsAdminController(ApplicationDbContext dbContext, ICollectionClientAdministrationService service, ILogger<CollectionClientsAdminController> logger)
    {
        _dbContext = dbContext;
        _service = service;
        _logger = logger;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index() => View(await _dbContext.CollectionClients.AsNoTracking().OrderBy(c => c.Name).ToListAsync());

    [HttpGet("create")]
    public IActionResult Create() => View();

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromForm] string name)
    {
        var result = await _service.CreateClientAsync(new(GetCurrentUserId(), name));
        if (result.IsFailure) { ModelState.AddModelError(string.Empty, result.Error); return View(); }
        return RedirectToAction(nameof(Details), new { id = result.Value!.Id });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Details(Guid id)
    {
        var client = await _dbContext.CollectionClients.AsNoTracking()
            .Include(c => c.AssignedUsers).ThenInclude(a => a.User)
            .Include(c => c.BankDetails)
            .Include(c => c.PurposeOptions)
            .Include(c => c.AmountOptions)
            .SingleOrDefaultAsync(c => c.Id == id);
        return client is null ? NotFound() : View(client);
    }

    [HttpPost("{id:guid}/users")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignUser(Guid id, [FromForm] Guid userId)
    {
        var result = await _service.AssignUserAsync(new(GetCurrentUserId(), id, userId));
        return RedirectWithError(nameof(Details), id, result.Error, result.IsFailure);
    }

    [HttpPost("{id:guid}/purpose-options")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddPurpose(Guid id, [FromForm] string name, [FromForm] int displayOrder)
    {
        var result = await _service.AddPurposeOptionAsync(new(GetCurrentUserId(), id, name, displayOrder));
        return RedirectWithError(nameof(Details), id, result.Error, result.IsFailure);
    }

    [HttpPost("{id:guid}/amount-options")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddAmount(Guid id, [FromForm] string name, [FromForm] decimal amount, [FromForm] int displayOrder)
    {
        var result = await _service.AddAmountOptionAsync(new(GetCurrentUserId(), id, name, amount, displayOrder));
        return RedirectWithError(nameof(Details), id, result.Error, result.IsFailure);
    }

    [HttpPost("{id:guid}/bank-details")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddBankDetail(Guid id, [FromForm] string accountName, [FromForm] string bankName, [FromForm] string branchCode, [FromForm] string accountNumber)
    {
        var result = await _service.AddBankDetailAsync(new(GetCurrentUserId(), id, accountName, bankName, branchCode, accountNumber));
        return RedirectWithError(nameof(Details), id, result.Error, result.IsFailure);
    }

    private IActionResult RedirectWithError(string action, Guid id, string error, bool hasError)
    {
        if (hasError) TempData["Error"] = error;
        return RedirectToAction(action, new { id });
    }

    private Guid GetCurrentUserId()
    {
        var rawId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("UserId");
        if (!Guid.TryParse(rawId, out var id)) _logger.LogWarning("Administrator client configuration request had no valid user id claim.");
        return id;
    }
}
