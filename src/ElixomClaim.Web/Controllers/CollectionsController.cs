using System.Security.Claims;
using ElixomClaim.Lib.Authorization;
using ElixomClaim.Lib.Data;
using ElixomClaim.Lib.Entities;
using ElixomClaim.Lib.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ElixomClaim.Web.Controllers;

[Authorize(Policy = PolicyNames.RequireTeller)]
[Route("collections")]
public class CollectionsController : Controller
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ICollectionService _collectionService;
    private readonly ILogger<CollectionsController> _logger;
    public CollectionsController(ApplicationDbContext dbContext, ICollectionService collectionService, ILogger<CollectionsController> logger) { _dbContext = dbContext; _collectionService = collectionService; _logger = logger; }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var since = DateTime.UtcNow.AddHours(-24);
        var collections = await _dbContext.CollectionTransactions.AsNoTracking().Include(c => c.CollectionClient).Include(c => c.PurposeOption).Where(c => c.TellerUserId == CurrentUserId() && c.CreatedAtUtc >= since).OrderByDescending(c => c.CreatedAtUtc).ToListAsync();
        return View(collections);
    }

    [HttpGet("create")]
    public async Task<IActionResult> Create()
    {
        await PopulateClientSelectionAsync();
        return View(new SelectCollectionClientInput());
    }

    [HttpPost("create/select-client")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SelectClient(SelectCollectionClientInput input)
    {
        if (input.CollectionClientId is { } clientId && await IsActiveClientAsync(clientId))
        {
            return RedirectToAction(nameof(CreateTransaction), new { clientId });
        }

        ModelState.AddModelError(nameof(input.CollectionClientId), "Choose an active client to continue.");
        await PopulateClientSelectionAsync();
        return View("Create", input);
    }

    [HttpGet("create/transaction")]
    public async Task<IActionResult> CreateTransaction(Guid? clientId)
    {
        if (clientId is not { } selectedClientId || !await IsActiveClientAsync(selectedClientId))
        {
            TempData["ErrorMessage"] = "Choose an active client before recording a collection.";
            return RedirectToAction(nameof(Create));
        }

        await PopulateOptionsAsync(selectedClientId);
        return View("Transaction", new RecordCollectionInput { CollectionClientId = selectedClientId });
    }

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(RecordCollectionInput input)
    {
        if (!await IsActiveClientAsync(input.CollectionClientId))
        {
            ModelState.AddModelError(nameof(input.CollectionClientId), "Choose an active client before recording a collection.");
            await PopulateClientSelectionAsync();
            return View("Create", new SelectCollectionClientInput());
        }

        if (input.PaymentDateUtc is null)
        {
            ModelState.AddModelError(nameof(input.PaymentDateLocal), "Enter a payment date and time.");
            await PopulateOptionsAsync(input.CollectionClientId);
            return View("Transaction", input);
        }

        var result = await _collectionService.RecordAsync(new(
            CurrentUserId(), input.CollectionClientId, input.PurposeOptionId, input.AmountOptionId, input.PayorName, input.PayorEmail,
            input.Method, 0m, input.PaymentDateUtc.Value, input.ReferenceNumber,
            input.PayorTelephone, input.Purpose, input.Amount > 0 ? input.Amount : null));
        if (result.IsFailure) { ModelState.AddModelError(string.Empty, result.Error); await PopulateOptionsAsync(input.CollectionClientId); return View("Transaction", input); }
        TempData["SuccessMessage"] = "Collection recorded and receipt queued.";
        return RedirectToAction(nameof(Details), new { id = result.Value!.Id });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Details(Guid id)
    {
        var collection = await FindVisibleCollectionAsync(id);
        return collection is null ? NotFound() : View(collection);
    }

    [HttpPost("{id:guid}/reissue")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reissue(Guid id)
    {
        var result = await _collectionService.ReissueReceiptAsync(id, CurrentUserId());
        TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] = result.IsSuccess ? "Receipt reissue queued." : result.Error;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet("{id:guid}/print")]
    public async Task<IActionResult> Print(Guid id)
    {
        var collection = await FindVisibleCollectionAsync(id);
        return collection is null ? NotFound() : View(collection);
    }

    private async Task<CollectionTransaction?> FindVisibleCollectionAsync(Guid id)
    {
        var collection = await _dbContext.CollectionTransactions.AsNoTracking()
            .Include(c => c.CollectionClient)
            .Include(c => c.PurposeOption)
            .Include(c => c.AmountOption)
            .Include(c => c.TellerUser)
            .SingleOrDefaultAsync(c => c.Id == id);
        if (collection is null) return null;
        var current = await _dbContext.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == CurrentUserId());
        return collection.TellerUserId == CurrentUserId() || current?.Role.HasMinimumRole(UserRole.Manager) == true ? collection : null;
    }

    private Task<List<CollectionClient>> ActiveClientsAsync() =>
        _dbContext.CollectionClients.AsNoTracking().Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync();

    private async Task PopulateClientSelectionAsync()
    {
        var clients = await ActiveClientsAsync();
        ViewBag.Clients = clients;
        ViewBag.UseClientCards = clients.Count <= 12;
    }

    private Task<bool> IsActiveClientAsync(Guid clientId) =>
        _dbContext.CollectionClients.AsNoTracking().AnyAsync(c => c.Id == clientId && c.IsActive);

    private async Task PopulateOptionsAsync(Guid clientId)
    {
        ViewBag.Client = await _dbContext.CollectionClients.AsNoTracking().SingleOrDefaultAsync(c => c.Id == clientId && c.IsActive);
        ViewBag.Purposes = await _dbContext.CollectionPurposeOptions.AsNoTracking().Where(o => o.IsActive && o.CollectionClientId == clientId).OrderBy(o => o.DisplayOrder).ToListAsync();
        ViewBag.Amounts = await _dbContext.CollectionAmountOptions.AsNoTracking().Where(o => o.IsActive && o.CollectionClientId == clientId).OrderBy(o => o.DisplayOrder).ToListAsync();
    }

    private Guid CurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("UserId");
        if (!Guid.TryParse(raw, out var id)) _logger.LogWarning("Collection request had no valid user identifier claim.");
        return id;
    }
}

public class SelectCollectionClientInput
{
    public Guid? CollectionClientId { get; set; }
}

public class RecordCollectionInput
{
    public Guid CollectionClientId { get; set; }
    // Retained for programmatic callers; the MVC form resolves suggestions from the entered values.
    public Guid? PurposeOptionId { get; set; }
    public Guid? AmountOptionId { get; set; }
    public string Purpose { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string PayorName { get; set; } = string.Empty;
    public string? PayorEmail { get; set; }
    public string? PayorTelephone { get; set; }
    public CollectionMethod Method { get; set; }
    public string? PaymentDateLocal { get; set; }
    public DateTime? PaymentDateUtc { get; set; }
    public string? ReferenceNumber { get; set; }
}
