using System.Security.Claims;
using ElixomClaim.Lib.Authorization;
using ElixomClaim.Lib.Data;
using ElixomClaim.Lib.Entities;
using ElixomClaim.Lib.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ElixomClaim.Web.Controllers;

[Authorize(Policy = PolicyNames.RequireActiveUser)]
[Route("claims")]
public class ClaimsController : Controller
{
    private readonly IClaimService _claimService;
    private readonly ApplicationDbContext _dbContext;

    public ClaimsController(IClaimService claimService, ApplicationDbContext dbContext)
    {
        _claimService = claimService;
        _dbContext = dbContext;
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
        return View(claims);
    }

    [HttpGet("create")]
    public IActionResult Create()
    {
        return View();
    }

    public record CreateClaimInput(string Title, string Description, decimal Amount);

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
        var claim = await _claimService.CreateDraftAsync(new CreateClaimCommand(userId, input.Title, input.Description, input.Amount));

        return RedirectToAction(nameof(Details), new { id = claim.Id });
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Details(long id)
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

    [HttpGet("{id:long}/edit")]
    public async Task<IActionResult> Edit(long id)
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

    [HttpPost("{id:long}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(long id, [FromForm] CreateClaimInput input)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(input.Title) || string.IsNullOrWhiteSpace(input.Description) || input.Amount <= 0)
        {
            ModelState.AddModelError("", "Title, description, and a positive amount are required.");
            return View();
        }

        var claim = await _claimService.EditDraftAsync(new EditClaimCommand(id, userId, input.Title, input.Description, input.Amount));
        if (claim == null)
        {
            return NotFound();
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("{id:long}/submit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(long id)
    {
        var userId = GetCurrentUserId();
        var success = await _claimService.SubmitAsync(new SubmitClaimCommand(id, userId));
        if (!success)
        {
            return BadRequest("Unable to submit claim.");
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("{id:long}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(long id)
    {
        var userId = GetCurrentUserId();
        var success = await _claimService.SoftDeleteAsync(new SoftDeleteClaimCommand(id, userId));
        if (!success)
        {
            return BadRequest("Unable to delete claim.");
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:long}/comment")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddComment(long id, [FromForm] string content)
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
