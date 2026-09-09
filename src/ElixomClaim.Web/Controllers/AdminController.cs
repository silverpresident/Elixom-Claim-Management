using ElixomClaim.Lib.Authorization;
using ElixomClaim.Lib.Data;
using ElixomClaim.Lib.Entities;
using ElixomClaim.Lib.Services;
using ElixomClaim.Web.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace ElixomClaim.Web.Controllers;

[Authorize(Policy = PolicyNames.RequireManager)]
[Route("admin")]
[EnableRateLimiting(RateLimitingConfiguration.MvcPolicy)]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly ILogger<AdminController> _logger;

    public AdminController(ApplicationDbContext dbContext, IAuditService auditService, ILogger<AdminController> logger)
    {
        _dbContext = dbContext;
        _auditService = auditService;
        _logger = logger;
    }

    [HttpGet("")]
    [Authorize(Policy = PolicyNames.RequireAdministrator)]
    public async Task<IActionResult> Index()
    {
        ViewBag.UserCount = await _dbContext.Users.CountAsync();
        ViewBag.RecentAuditCount = await _dbContext.AuditRecords.CountAsync(record => record.OccurredAtUtc >= DateTime.UtcNow.AddDays(-7));
        ViewBag.PendingEmailCount = await _dbContext.EmailOutboxItems.CountAsync(item => item.Status == EmailOutboxStatus.Pending || item.Status == EmailOutboxStatus.Processing);
        return View();
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

        _logger.LogInformation("Administrator updated user {UserId} role/status", user.Id);

        return RedirectToAction(nameof(Users));
    }

    [HttpGet("audit-logs")]
    [Authorize(Policy = PolicyNames.RequireManager)]
    public async Task<IActionResult> AuditLogs()
    {
        var isAdministrator = User.IsInRole(UserRole.Administrator.ToString());
        var query = _dbContext.AuditRecords.AsNoTracking();
        if (!isAdministrator)
        {
            query = query.Where(record => record.EntityType == "Claim" || record.EntityType == "CollectionTransaction" || record.EntityType == "JobPayment");
        }

        var records = await query
            .OrderByDescending(a => a.OccurredAtUtc)
            .Take(200)
            .ToListAsync();

        var viewModels = records.Select(r => new Models.AuditRecordViewModel
        {
            Id = r.Id,
            ActorEmail = r.ActorEmail,
            Action = r.Action,
            EntityType = r.EntityType,
            EntityId = r.EntityId,
            CorrelationId = r.CorrelationId,
            IpAddress = r.IpAddress,
            IsMcpOperation = r.IsMcpOperation,
            OccurredAtUtc = r.OccurredAtUtc,
            // Strict projection: Manager sees operational metadata only; never email body, bank details, or state details
            BeforeStateJson = isAdministrator ? r.BeforeStateJson : null,
            AfterStateJson = isAdministrator ? r.AfterStateJson : null
        }).ToList();

        ViewBag.IsAdministrator = isAdministrator;
        return View(viewModels);
    }

    [HttpGet("emails")]
    [Authorize(Policy = PolicyNames.RequireAdministrator)]
    public async Task<IActionResult> EmailLogs()
    {
        var records = await _dbContext.EmailLogs.AsNoTracking()
            .OrderByDescending(log => log.CreatedAtUtc)
            .Take(200)
            .ToListAsync();
        return View(records);
    }

    [HttpGet("emails/{id:guid}")]
    [Authorize(Policy = PolicyNames.RequireAdministrator)]
    public async Task<IActionResult> EmailLog(Guid id)
    {
        var record = await _dbContext.EmailLogs.AsNoTracking().SingleOrDefaultAsync(log => log.Id == id);
        return record is null ? NotFound() : View(record);
    }
}
