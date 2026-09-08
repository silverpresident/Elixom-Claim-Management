using System.Security.Claims;
using ElixomClaim.Lib.Authorization;
using ElixomClaim.Lib.Data;
using ElixomClaim.Lib.Services;
using ElixomClaim.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ElixomClaim.Web.Controllers;

[Authorize(Policy = PolicyNames.RequireAccountant)]
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
    public async Task<IActionResult> Create([FromForm] string name, [FromForm] string? description, [FromForm] string? notes, [FromForm] decimal perJobProcessingFee, [FromForm] decimal perTransactionFee)
    {
        var result = await _service.CreateClientAsync(new(GetCurrentUserId(), name, description, notes, perJobProcessingFee, perTransactionFee));
        if (result.IsFailure) { ModelState.AddModelError(string.Empty, result.Error); return View(); }
        return RedirectToAction(nameof(Details), new { id = result.Value!.Id });
    }

    [HttpPost("{id:guid}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(Guid id, [FromForm] string name, [FromForm] string? description, [FromForm] string? notes, [FromForm] decimal perJobProcessingFee, [FromForm] decimal perTransactionFee)
    {
        var result = await _service.UpdateClientAsync(new(GetCurrentUserId(), id, name, description, notes, perJobProcessingFee, perTransactionFee));
        return RedirectWithError(nameof(Details), id, result.Error, result.IsFailure);
    }

    [HttpPost("{id:guid}/active")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetActive(Guid id, [FromForm] bool isActive)
    {
        var result = await _service.SetClientActiveAsync(new(GetCurrentUserId(), id, isActive));
        return RedirectWithError(nameof(Details), id, result.Error, result.IsFailure);
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
        if (client is null)
        {
            return NotFound();
        }

        var availableUsers = await _dbContext.Users.AsNoTracking()
            .Where(user => user.IsActive && !_dbContext.CollectionClientUsers
                .Any(assignment => assignment.CollectionClientId == client.Id && assignment.UserId == user.Id))
            .OrderBy(user => user.DisplayName ?? user.FullName)
            .ThenBy(user => user.Email)
            .Select(user => new CollectionClientAssignableUser(
                user.Id,
                string.IsNullOrWhiteSpace(user.DisplayName) ? user.FullName : user.DisplayName,
                user.Email))
            .ToListAsync();

        return View(new CollectionClientDetailsViewModel(client, availableUsers));
    }

    [HttpPost("{id:guid}/users")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = PolicyNames.RequireAdministrator)]
    public async Task<IActionResult> AssignUser(Guid id, [FromForm] Guid userId)
    {
        var result = await _service.AssignUserAsync(new(GetCurrentUserId(), id, userId));
        return RedirectWithError(nameof(Details), id, result.Error, result.IsFailure);
    }

    [HttpPost("{id:guid}/purpose-options")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = PolicyNames.RequireAdministrator)]
    public async Task<IActionResult> AddPurpose(Guid id, [FromForm] string name, [FromForm] int displayOrder)
    {
        var result = await _service.AddPurposeOptionAsync(new(GetCurrentUserId(), id, name, displayOrder));
        return RedirectWithError(nameof(Details), id, result.Error, result.IsFailure);
    }

    [HttpPost("{id:guid}/amount-options")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = PolicyNames.RequireAdministrator)]
    public async Task<IActionResult> AddAmount(Guid id, [FromForm] string name, [FromForm] decimal amount, [FromForm] int displayOrder)
    {
        var result = await _service.AddAmountOptionAsync(new(GetCurrentUserId(), id, name, amount, displayOrder));
        return RedirectWithError(nameof(Details), id, result.Error, result.IsFailure);
    }

    [HttpPost("{id:guid}/bank-details")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = PolicyNames.RequireAdministrator)]
    public async Task<IActionResult> AddBankDetail(Guid id, [FromForm] string accountName, [FromForm] string bankName, [FromForm] string branchCode, [FromForm] string branchName, [FromForm] string accountType, [FromForm] string accountNumber, [FromForm] string? notes)
    {
        var result = await _service.AddBankDetailAsync(new(GetCurrentUserId(), id, accountName, bankName, branchCode, branchName, accountType, accountNumber, notes));
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
        if (!Guid.TryParse(rawId, out var id)) _logger.LogWarning("Collection client management request had no valid user id claim.");
        return id;
    }
}
