using ElixomClaim.Lib.Authorization;
using ElixomClaim.Lib.Data;
using ElixomClaim.Lib.Entities;
using ElixomClaim.Lib.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ElixomClaim.Web.Controllers;

[Authorize(Policy = PolicyNames.RequireManager)]
[Route("admin")]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IAuditService _auditService;

    public AdminController(ApplicationDbContext dbContext, IAuditService auditService)
    {
        _dbContext = dbContext;
        _auditService = auditService;
    }

    [HttpGet("users")]
    [Authorize(Policy = PolicyNames.RequireAdministrator)]
    public async Task<IActionResult> Users()
    {
        var users = await _dbContext.Users.OrderBy(u => u.Email).ToListAsync();
        return View(users);
    }

    [HttpGet("users/{id:guid}/edit")]
    [Authorize(Policy = PolicyNames.RequireAdministrator)]
    public async Task<IActionResult> EditUser(Guid id)
    {
        var user = await _dbContext.Users.FindAsync(id);
        if (user == null)
        {
            return NotFound();
        }
        return View(user);
    }

    [HttpPost("users/{id:guid}/edit")]
    [Authorize(Policy = PolicyNames.RequireAdministrator)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditUser(Guid id, [FromForm] UserRole role, [FromForm] bool isActive)
    {
        var user = await _dbContext.Users.FindAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        var oldRole = user.Role;
        var oldIsActive = user.IsActive;

        user.Role = role;
        user.IsActive = isActive;
        user.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        await _auditService.LogAsync(
            action: "USER_ROLE_OR_STATUS_UPDATED",
            target: $"User:{user.Id}",
            beforeState: new { role = oldRole, isActive = oldIsActive },
            afterState: new { role = user.Role, isActive = user.IsActive },
            actorUserId: User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            actorEmail: User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value);

        return RedirectToAction(nameof(Users));
    }

    [HttpGet("audit-logs")]
    [Authorize(Policy = PolicyNames.RequireManager)]
    public async Task<IActionResult> AuditLogs()
    {
        var records = await _dbContext.AuditRecords
            .OrderByDescending(a => a.TimestampUtc)
            .Take(200)
            .ToListAsync();

        var isAdministrator = User.IsInRole(UserRole.Administrator.ToString());

        var viewModels = records.Select(r => new Models.AuditRecordViewModel
        {
            Id = r.Id,
            ActorEmail = r.ActorEmail,
            Action = r.Action,
            Target = r.Target,
            CorrelationId = r.CorrelationId,
            IpAddress = r.IpAddress,
            IsMcpOperation = r.IsMcpOperation,
            TimestampUtc = r.TimestampUtc,
            // Strict projection: Manager sees operational metadata only; never email body, bank details, or state details
            BeforeStateJson = isAdministrator ? r.BeforeStateJson : null,
            AfterStateJson = isAdministrator ? r.AfterStateJson : null
        }).ToList();

        ViewBag.IsAdministrator = isAdministrator;
        return View(viewModels);
    }
}
