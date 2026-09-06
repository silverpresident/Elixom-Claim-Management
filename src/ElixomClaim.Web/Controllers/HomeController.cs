using System.Diagnostics;
using System.Security.Claims;
using ElixomClaim.Lib.Data;
using ElixomClaim.Lib.Entities;
using ElixomClaim.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ElixomClaim.Web.Controllers;

public class HomeController : Controller
{
    private readonly ApplicationDbContext _dbContext;

    public HomeController(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IActionResult> Index()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return View("AnonymousLanding");
        }

        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("UserId");
        Guid.TryParse(userIdStr, out var userId);
        var user = await _dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);

        var viewModel = new HomeDashboardViewModel
        {
            CurrentUser = user,
            UserClaimsCount = userId != Guid.Empty ? await _dbContext.Claims.CountAsync(c => c.ClaimantUserId == userId && !c.IsDeleted) : 0,
            PendingClaimsCount = await _dbContext.Claims.CountAsync(c => c.Status == ClaimStatus.Submitted && !c.IsDeleted),
            ActiveCollections24hCount = await _dbContext.CollectionTransactions.CountAsync(c => c.CreatedAtUtc >= DateTime.UtcNow.AddHours(-24)),
            ProcessingJobsCount = await _dbContext.JobPayments.CountAsync(j => j.Status == JobPaymentStatus.Processing),
            SubmittedJobsCount = await _dbContext.JobPayments.CountAsync(j => j.Status == JobPaymentStatus.Submitted || j.Status == JobPaymentStatus.Scheduled),
            GeneratedPayrollsCount = await _dbContext.Payrolls.CountAsync(p => p.Status == PayrollStatus.Generated)
        };

        return View(viewModel);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}

public class HomeDashboardViewModel
{
    public User? CurrentUser { get; set; }
    public int UserClaimsCount { get; set; }
    public int PendingClaimsCount { get; set; }
    public int ActiveCollections24hCount { get; set; }
    public int ProcessingJobsCount { get; set; }
    public int SubmittedJobsCount { get; set; }
    public int GeneratedPayrollsCount { get; set; }
}
