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
        await PopulateOptionsAsync();
        return View(new RecordCollectionInput { PaymentDateUtc = DateTime.UtcNow });
    }

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(RecordCollectionInput input)
    {
        var result = await _collectionService.RecordAsync(new(CurrentUserId(), input.CollectionClientId, input.PurposeOptionId, input.AmountOptionId, input.PayorName, input.PayorEmail, input.Method, input.ProcessingFee, DateTime.SpecifyKind(input.PaymentDateUtc, DateTimeKind.Utc), input.ReferenceNumber));
        if (result.IsFailure) { ModelState.AddModelError(string.Empty, result.Error); await PopulateOptionsAsync(); return View(input); }
        TempData["SuccessMessage"] = "Collection recorded and receipt queued.";
        return RedirectToAction(nameof(Details), new { id = result.Value!.Id });
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Details(long id)
    {
        var collection = await FindVisibleCollectionAsync(id);
        return collection is null ? NotFound() : View(collection);
    }

    [HttpPost("{id:long}/reissue")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reissue(long id)
    {
        var result = await _collectionService.ReissueReceiptAsync(id, CurrentUserId());
        TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] = result.IsSuccess ? "Receipt reissue queued." : result.Error;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet("{id:long}/print")]
    public async Task<IActionResult> Print(long id)
    {
        var collection = await FindVisibleCollectionAsync(id);
        return collection is null ? NotFound() : View(collection);
    }

    private async Task<CollectionTransaction?> FindVisibleCollectionAsync(long id)
    {
        var collection = await _dbContext.CollectionTransactions.AsNoTracking().Include(c => c.CollectionClient).Include(c => c.PurposeOption).Include(c => c.AmountOption).SingleOrDefaultAsync(c => c.Id == id);
        if (collection is null) return null;
        var current = await _dbContext.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == CurrentUserId());
        return collection.TellerUserId == CurrentUserId() || current?.Role.HasMinimumRole(UserRole.Manager) == true ? collection : null;
    }

    private async Task PopulateOptionsAsync()
    {
        ViewBag.Clients = await _dbContext.CollectionClients.AsNoTracking().Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync();
        ViewBag.Purposes = await _dbContext.CollectionPurposeOptions.AsNoTracking().Where(o => o.IsActive).OrderBy(o => o.DisplayOrder).ToListAsync();
        ViewBag.Amounts = await _dbContext.CollectionAmountOptions.AsNoTracking().Where(o => o.IsActive).OrderBy(o => o.DisplayOrder).ToListAsync();
    }

    private Guid CurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("UserId");
        if (!Guid.TryParse(raw, out var id)) _logger.LogWarning("Collection request had no valid user identifier claim.");
        return id;
    }
}

public class RecordCollectionInput
{
    public Guid CollectionClientId { get; set; }
    public long PurposeOptionId { get; set; }
    public long AmountOptionId { get; set; }
    public string PayorName { get; set; } = string.Empty;
    public string? PayorEmail { get; set; }
    public CollectionMethod Method { get; set; }
    public decimal ProcessingFee { get; set; }
    public DateTime PaymentDateUtc { get; set; }
    public string? ReferenceNumber { get; set; }
}
