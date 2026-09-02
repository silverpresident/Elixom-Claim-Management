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
[Route("job-payments")]
public class JobPaymentsController : Controller
{
    private readonly ApplicationDbContext _db; private readonly IJobPaymentService _service;
    public JobPaymentsController(ApplicationDbContext db, IJobPaymentService service) { _db = db; _service = service; }
    [HttpGet("")] public async Task<IActionResult> Index(JobPaymentStatus? status) => View(await _db.JobPayments.AsNoTracking().Include(j => j.PayeeUser).Include(j => j.CollectionClient).Where(j => !status.HasValue || j.Status == status).OrderByDescending(j => j.CreatedAtUtc).ToListAsync());
    [HttpGet("accountant-queue")][Authorize(Policy = PolicyNames.RequireAccountant)] public async Task<IActionResult> AccountantQueue() => View(await _db.JobPayments.AsNoTracking().Include(j => j.PayeeUser).Include(j => j.CollectionClient).Where(j => j.Status == JobPaymentStatus.Submitted || j.Status == JobPaymentStatus.Scheduled).OrderBy(j => j.ScheduledAtUtc).ThenBy(j => j.CreatedAtUtc).ToListAsync());
    [HttpGet("{id:long}")] public async Task<IActionResult> Details(long id) { var job = await QueryJob().SingleOrDefaultAsync(j => j.Id == id); return job is null ? NotFound() : View(job); }
    [HttpGet("{id:long}/collections")] public async Task<IActionResult> Collections(long id) { var job = await _db.JobPayments.AsNoTracking().SingleOrDefaultAsync(j => j.Id == id); if (job?.CollectionClientId is null) return NotFound(); ViewBag.Job = job; return View(await _db.CollectionTransactions.AsNoTracking().Include(c => c.PurposeOption).Where(c => c.CollectionClientId == job.CollectionClientId && c.Status == CollectionStatus.Collected).ToListAsync()); }
    [HttpPost("{id:long}/claims/{claimId:long}")][ValidateAntiForgeryToken] public async Task<IActionResult> AttachClaim(long id, long claimId) => RedirectResult(await _service.AttachClaimAsync(new(CurrentUserId(), id, claimId)), id);
    [HttpPost("{id:long}/collections/{collectionId:long}")][ValidateAntiForgeryToken] public async Task<IActionResult> AttachCollection(long id, long collectionId) => RedirectResult(await _service.AttachCollectionAsync(new(CurrentUserId(), id, collectionId)), id);
    [HttpPost("{id:long}/resend")][ValidateAntiForgeryToken] public async Task<IActionResult> Resend(long id) => RedirectResult(await _service.ResendNotificationAsync(id, CurrentUserId()), id);
    [HttpGet("{id:long}/print")] public async Task<IActionResult> Print(long id) { var job = await QueryJob().SingleOrDefaultAsync(j => j.Id == id); return job is null ? NotFound() : View(job); }
    private IQueryable<JobPayment> QueryJob() => _db.JobPayments.AsNoTracking().Include(j => j.PayeeUser).Include(j => j.CollectionClient).Include(j => j.Claims).ThenInclude(x => x.Claim).Include(j => j.Collections).ThenInclude(x => x.CollectionTransaction).Include(j => j.Deductions);
    private IActionResult RedirectResult(ElixomClaim.Lib.Common.Result result, long id) { TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] = result.IsSuccess ? "Job payment updated." : result.Error; return RedirectToAction(nameof(Details), new { id }); }
    private Guid CurrentUserId() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("UserId"), out var id) ? id : Guid.Empty;
}
