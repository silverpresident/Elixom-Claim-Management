using System.Diagnostics;
using System.Security.Claims;
using ElixomClaim.Lib.Data;
using ElixomClaim.Lib.Entities;
using ElixomClaim.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ElixomClaim.Web.Controllers;

public class HomeController : Controller
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<HomeController> _logger;

    public HomeController(ApplicationDbContext dbContext, ILogger<HomeController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            _logger.LogDebug("Anonymous user requested the home landing page.");
            return View("AnonymousLanding");
        }

        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("UserId");
        Guid.TryParse(userIdStr, out var userId);
        var user = await _dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);

        _logger.LogInformation("Home dashboard requested by actor {ActorId}; active user resolved {UserResolved}.", userId, user is not null);

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
        _logger.LogDebug("Privacy page requested.");
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        var requestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        _logger.LogWarning("Error page rendered for request {RequestId}.", requestId);
        return View(new ErrorViewModel { RequestId = requestId });
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
