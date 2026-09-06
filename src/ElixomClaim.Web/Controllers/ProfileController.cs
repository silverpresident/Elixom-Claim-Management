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

namespace ElixomClaim.Web.Controllers;

[Authorize(Policy = PolicyNames.RequireActiveUser)]
[Route("profile")]
[EnableRateLimiting(RateLimitingConfiguration.MvcPolicy)]
public class ProfileController : Controller
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IAuditService _auditService;

    public ProfileController(ApplicationDbContext dbContext, IAuditService auditService)
    {
        _dbContext = dbContext;
        _auditService = auditService;
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
        string? BankBranchCode
    );

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
            string.IsNullOrWhiteSpace(input.BankBranchCode))
        {
            TempData["ErrorMessage"] = "All bank details fields (Bank Name, Account Name, Account Number, and Branch Code) are required.";
            return RedirectToAction(nameof(Index));
        }

        var beforeState = new
        {
            BankAccountName = user.BankAccountName,
            BankAccountNumber = user.GetMaskedBankAccountNumber(),
            BankName = user.BankName,
            BankBranchCode = user.BankBranchCode
        };

        user.BankAccountName = input.BankAccountName.Trim();
        user.BankAccountNumber = input.BankAccountNumber.Trim();
        user.BankName = input.BankName.Trim();
        user.BankBranchCode = input.BankBranchCode.Trim();
        user.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        var afterState = new
        {
            BankAccountName = user.BankAccountName,
            BankAccountNumber = user.GetMaskedBankAccountNumber(),
            BankName = user.BankName,
            BankBranchCode = user.BankBranchCode
        };

        await _auditService.LogAsync(
            action: "USER_BANK_DETAILS_UPDATED",
            target: $"User:{user.Id}",
            beforeState: beforeState,
            afterState: afterState,
            actorUserId: user.Id.ToString(),
            actorEmail: user.Email);

        TempData["SuccessMessage"] = "Your bank details have been updated successfully.";
        return RedirectToAction(nameof(Index));
    }
}
