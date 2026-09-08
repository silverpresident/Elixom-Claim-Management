using System.Security.Claims;
using ElixomClaim.Lib.Authorization;
using ElixomClaim.Lib.Data;
using ElixomClaim.Lib.Entities;
using ElixomClaim.Lib.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ElixomClaim.Web.Controllers;

[Authorize(Policy = PolicyNames.RequireActiveUser)]
[Route("claims")]
public class ClaimsController : Controller
{
    private readonly IClaimService _claimService;
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<ClaimsController> _logger;

    public ClaimsController(IClaimService claimService, ApplicationDbContext dbContext, ILogger<ClaimsController>? logger = null)
    {
        _claimService = claimService;
        _dbContext = dbContext;
        _logger = logger ?? NullLogger<ClaimsController>.Instance;
    }

    private Guid GetCurrentUserId()
    {
        var idStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("UserId")?.Value;
        return Guid.TryParse(idStr, out var id) ? id : Guid.Empty;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var userId = GetCurrentUserId();
        var claims = await _claimService.GetUserClaimsAsync(userId);
        var paymentHistory = await _dbContext.JobPayments
            .Where(j => j.PayeeUserId == userId)
            .OrderByDescending(j => j.PaymentDateUtc ?? j.CreatedAtUtc)
            .Take(5)
            .ToListAsync();

        var viewModel = new Models.UserDashboardViewModel
        {
            Claims = claims,
            PaymentHistory = paymentHistory
        };

        return View(viewModel);
    }

    [HttpGet("payment-history")]
    public async Task<IActionResult> PaymentHistory()
    {
        var userId = GetCurrentUserId();
        var payments = await _dbContext.JobPayments.AsNoTracking()
            .Where(job => job.PayeeUserId == userId)
            .OrderByDescending(job => job.PaymentDateUtc ?? job.CreatedAtUtc)
            .ToListAsync();
        return View(payments);
    }

    [HttpGet("create")]
    public IActionResult Create()
    {
        return View();
    }

    public record CreateClaimInput(string Title, string Description, decimal Amount, DateTime? DateOfJob = null);

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromForm] CreateClaimInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Title) || string.IsNullOrWhiteSpace(input.Description) || input.Amount <= 0)
        {
            ModelState.AddModelError("", "Title, description, and an amount greater than $0.00 are required.");
            return View(input);
        }

        var userId = GetCurrentUserId();
        var claim = await _claimService.CreateDraftAsync(new CreateClaimCommand(userId, input.Title, input.Description, input.Amount, input.DateOfJob));
        _logger.LogInformation("Claim {ClaimId} draft created by {ActorId}", claim.Id, userId);

        return RedirectToAction(nameof(Details), new { id = claim.Id });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Details(Guid id)
    {
        var userId = GetCurrentUserId();
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
        {
            return Unauthorized();
        }

        var claim = await _claimService.GetByIdAsync(id, user);
        if (claim == null)
        {
            return NotFound();
        }

        return View(claim);
    }

    [HttpGet("{id:guid}/edit")]
    public async Task<IActionResult> Edit(Guid id)
    {
        var userId = GetCurrentUserId();
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
        {
            return Unauthorized();
        }

        var claim = await _claimService.GetByIdAsync(id, user);
        if (claim == null || claim.ClaimantUserId != userId || claim.Status != ClaimStatus.Draft)
        {
            return BadRequest("Only draft claims can be edited.");
        }

        return View(claim);
    }

    [HttpPost("{id:guid}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, [FromForm] CreateClaimInput input)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(input.Title) || string.IsNullOrWhiteSpace(input.Description) || input.Amount <= 0)
        {
            ModelState.AddModelError("", "Title, description, and a positive amount are required.");
            return View();
        }

        var claim = await _claimService.EditDraftAsync(new EditClaimCommand(id, userId, input.Title, input.Description, input.Amount, input.DateOfJob));
        if (claim == null)
        {
            return NotFound();
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("{id:guid}/submit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(Guid id)
    {
        var userId = GetCurrentUserId();
        var success = await _claimService.SubmitAsync(new SubmitClaimCommand(id, userId));
        if (!success)
        {
            return BadRequest("Unable to submit claim.");
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("{id:guid}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userId = GetCurrentUserId();
        var success = await _claimService.SoftDeleteAsync(new SoftDeleteClaimCommand(id, userId));
        if (!success)
        {
            return BadRequest("Unable to delete claim.");
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:guid}/comment")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddComment(Guid id, [FromForm] string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return RedirectToAction(nameof(Details), new { id });
        }

        var userId = GetCurrentUserId();
        await _claimService.AddCommentAsync(new AddClaimCommentCommand(id, userId, content, IsPrivate: false));

        return RedirectToAction(nameof(Details), new { id });
    }
}
