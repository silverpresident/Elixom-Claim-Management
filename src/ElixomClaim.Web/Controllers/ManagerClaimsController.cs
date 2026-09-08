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

[Authorize(Policy = PolicyNames.RequireManager)]
[Route("manager/claims")]
public class ManagerClaimsController : Controller
{
    private readonly IClaimService _claimService;
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<ManagerClaimsController> _logger;

    public ManagerClaimsController(IClaimService claimService, ApplicationDbContext dbContext, ILogger<ManagerClaimsController>? logger = null)
    {
        _claimService = claimService;
        _dbContext = dbContext;
        _logger = logger ?? NullLogger<ManagerClaimsController>.Instance;
    }

    private Guid GetCurrentUserId()
    {
        var idStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("UserId")?.Value;
        return Guid.TryParse(idStr, out var id) ? id : Guid.Empty;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] ClaimStatus? status = ClaimStatus.Submitted)
    {
        var claims = await _claimService.GetQueueClaimsAsync(status);
        ViewBag.CurrentStatusFilter = status;
        return View(claims);
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

    [HttpPost("{id:guid}/accept")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Accept(Guid id)
    {
        var userId = GetCurrentUserId();
        var success = await _claimService.AcceptAsync(new AcceptClaimCommand(id, userId));
        if (!success)
        {
            return BadRequest("Unable to accept claim.");
        }
        _logger.LogInformation("Claim {ClaimId} accepted by {ActorId}", id, userId);

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("{id:guid}/reject")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(Guid id, [FromForm] string rejectionReason)
    {
        if (string.IsNullOrWhiteSpace(rejectionReason))
        {
            ModelState.AddModelError("", "Rejection reason is required.");
            return RedirectToAction(nameof(Details), new { id });
        }

        var userId = GetCurrentUserId();
        var success = await _claimService.RejectAsync(new RejectClaimCommand(id, userId, rejectionReason));
        if (!success)
        {
            return BadRequest("Unable to reject claim.");
        }
        _logger.LogInformation("Claim {ClaimId} rejected by {ActorId}", id, userId);

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("{id:guid}/comment")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddComment(Guid id, [FromForm] string content, [FromForm] bool isPrivate)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return RedirectToAction(nameof(Details), new { id });
        }

        var userId = GetCurrentUserId();
        await _claimService.AddCommentAsync(new AddClaimCommentCommand(id, userId, content, isPrivate));
        _logger.LogInformation("Comment added to claim {ClaimId} by {ActorId} with private status {IsPrivate}", id, userId, isPrivate);

        return RedirectToAction(nameof(Details), new { id });
    }
}
