using System.Security.Claims;
using ElixomClaim.Lib.Authorization;
using ElixomClaim.Lib.Data;
using ElixomClaim.Lib.Entities;
using ElixomClaim.Lib.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ElixomClaim.Web.Controllers;

[Authorize(Policy = PolicyNames.RequireManager)]
[Route("manager/claims")]
public class ManagerClaimsController : Controller
{
    private readonly IClaimService _claimService;
    private readonly ApplicationDbContext _dbContext;

    public ManagerClaimsController(IClaimService claimService, ApplicationDbContext dbContext)
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
    public async Task<IActionResult> Index([FromQuery] ClaimStatus? status = ClaimStatus.Submitted)
    {
        var claims = await _claimService.GetQueueClaimsAsync(status);
        ViewBag.CurrentStatusFilter = status;
        return View(claims);
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

    [HttpPost("{id:long}/accept")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Accept(long id)
    {
        var userId = GetCurrentUserId();
        var success = await _claimService.AcceptAsync(new AcceptClaimCommand(id, userId));
        if (!success)
        {
            return BadRequest("Unable to accept claim.");
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("{id:long}/reject")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(long id, [FromForm] string rejectionReason)
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

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("{id:long}/comment")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddComment(long id, [FromForm] string content, [FromForm] bool isPrivate)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return RedirectToAction(nameof(Details), new { id });
        }

        var userId = GetCurrentUserId();
        await _claimService.AddCommentAsync(new AddClaimCommentCommand(id, userId, content, isPrivate));

        return RedirectToAction(nameof(Details), new { id });
    }
}
