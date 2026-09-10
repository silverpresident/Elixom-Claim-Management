using System.Security.Claims;
using ElixomClaim.Lib.Authorization;
using ElixomClaim.Lib.Data;
using ElixomClaim.Lib.Entities;
using ElixomClaim.Lib.Services;
using ElixomClaim.Web.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ElixomClaim.Web.Controllers;

[Authorize(Policy = PolicyNames.RequireActiveUser)]
[Route("profile")]
[EnableRateLimiting(RateLimitingConfiguration.MvcPolicy)]
public class ProfileController : Controller
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly ILogger<ProfileController> _logger;

    public ProfileController(ApplicationDbContext dbContext, IAuditService auditService, ILogger<ProfileController> logger)
    {
        _dbContext = dbContext;
        _auditService = auditService;
        _logger = logger;
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
        var user = await _dbContext.Users.SingleOrDefaultAsync(u => u.Id == userId);
        if (user == null || !user.IsActive)
        {
            return Unauthorized();
        }

        var isAccountantOrAdmin = User.IsInRole(UserRole.Accountant.ToString()) || User.IsInRole(UserRole.Administrator.ToString());
        var summary = UserProfileSummary.FromUser(user, includeFullBankDetails: isAccountantOrAdmin);

        ViewBag.User = user;
        ViewBag.Summary = summary;
        ViewBag.IsAccountantOrAdmin = isAccountantOrAdmin;

        return View(user);
    }

    public record UpdateBankDetailsInput(
        string? BankAccountName,
        string? BankAccountNumber,
        string? BankName,
        string? BankBranchCode,
        string? BankBranchName,
        string? BankAccountType
    );

    public record UpdateDisplayNameInput(string? DisplayName);

    [HttpPost("display-name")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateDisplayName([FromForm] UpdateDisplayNameInput input)
    {
        var userId = GetCurrentUserId();
        var user = await _dbContext.Users.SingleOrDefaultAsync(u => u.Id == userId);
        if (user == null || !user.IsActive)
        {
            return Unauthorized();
        }

        var displayName = input.DisplayName?.Trim();
        if (displayName?.Length > 100)
        {
            TempData["ErrorMessage"] = "Display name must be 100 characters or fewer.";
            return RedirectToAction(nameof(Index));
        }

        var beforeState = new { user.DisplayName };
        user.DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName;
        user.UpdatedAtUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();
        await _auditService.LogAsync("USER_DISPLAY_NAME_UPDATED", new AuditEntity("User", user.Id.ToString()), beforeState, new { user.DisplayName }, user.Id.ToString(), user.Email);
        _logger.LogInformation("User profile display name updated for {UserId}", user.Id);

        TempData["SuccessMessage"] = "Your display name has been updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("bank-details")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateBankDetails([FromForm] UpdateBankDetailsInput input)
    {
        var userId = GetCurrentUserId();
        var user = await _dbContext.Users.SingleOrDefaultAsync(u => u.Id == userId);
        if (user == null || !user.IsActive)
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(input.BankName) ||
            string.IsNullOrWhiteSpace(input.BankAccountName) ||
            string.IsNullOrWhiteSpace(input.BankAccountNumber) ||
            string.IsNullOrWhiteSpace(input.BankBranchCode) ||
            string.IsNullOrWhiteSpace(input.BankBranchName) ||
            string.IsNullOrWhiteSpace(input.BankAccountType))
        {
            TempData["ErrorMessage"] = "All bank details fields (Bank Name, Account Name, Account Number, Branch Code, Branch Name, and Account Type) are required.";
            return RedirectToAction(nameof(Index));
        }

        var beforeState = new
        {
            BankAccountName = user.BankAccountName,
            BankAccountNumber = user.GetMaskedBankAccountNumber(),
            BankName = user.BankName,
            BankBranchCode = user.BankBranchCode,
            BankBranchName = user.BankBranchName,
            BankAccountType = user.BankAccountType
        };

        user.BankAccountName = input.BankAccountName.Trim();
        user.BankAccountNumber = input.BankAccountNumber.Trim();
        user.BankName = input.BankName.Trim();
        user.BankBranchCode = input.BankBranchCode.Trim();
        user.BankBranchName = input.BankBranchName.Trim();
        user.BankAccountType = input.BankAccountType.Trim();
        user.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        var afterState = new
        {
            BankAccountName = user.BankAccountName,
            BankAccountNumber = user.GetMaskedBankAccountNumber(),
            BankName = user.BankName,
            BankBranchCode = user.BankBranchCode,
            BankBranchName = user.BankBranchName,
            BankAccountType = user.BankAccountType
        };

        await _auditService.LogAsync(
            action: "USER_BANK_DETAILS_UPDATED",
            entity: new AuditEntity("User", user.Id.ToString()),
            beforeState: beforeState,
            afterState: afterState,
            actorUserId: user.Id.ToString(),
            actorEmail: user.Email);
        _logger.LogInformation("User payout bank details updated for {UserId}", user.Id);

        TempData["SuccessMessage"] = "Your bank details have been updated successfully.";
        return RedirectToAction(nameof(Index));
    }
}
