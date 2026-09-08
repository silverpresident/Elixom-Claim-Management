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
[Route("job-payments")]
public class JobPaymentsController : Controller
{
    private readonly ApplicationDbContext _db; private readonly IJobPaymentService _service; private readonly ILogger<JobPaymentsController> _logger;
    public JobPaymentsController(ApplicationDbContext db, IJobPaymentService service, ILogger<JobPaymentsController>? logger = null) { _db = db; _service = service; _logger = logger ?? NullLogger<JobPaymentsController>.Instance; }
    [HttpGet("")] public async Task<IActionResult> Index(JobPaymentStatus? status) => View(await _db.JobPayments.AsNoTracking().Include(j => j.PayeeUser).Include(j => j.CollectionClient).Where(j => !status.HasValue || j.Status == status).OrderByDescending(j => j.CreatedAtUtc).ToListAsync());
    [HttpGet("accountant-queue")][Authorize(Policy = PolicyNames.RequireAccountant)] public async Task<IActionResult> AccountantQueue() => View(await _db.JobPayments.AsNoTracking().Include(j => j.PayeeUser).Include(j => j.CollectionClient).Where(j => j.Status == JobPaymentStatus.Submitted || j.Status == JobPaymentStatus.Scheduled).OrderBy(j => j.ScheduledAtUtc).ThenBy(j => j.CreatedAtUtc).ToListAsync());
    [HttpGet("create")]
    public async Task<IActionResult> Create([FromQuery] Guid? payeeUserId, [FromQuery] Guid? collectionClientId)
    {
        await PopulatePayeesAsync();
        ViewBag.SelectedPayeeUserId = payeeUserId;
        ViewBag.SelectedCollectionClientId = collectionClientId;
        return View();
    }

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromForm] Guid? payeeUserId, [FromForm] Guid? collectionClientId, [FromForm] string? title, [FromForm] string? publicNote, [FromForm] string? internalNote)
    {
        var result = await _service.CreateAsync(new CreateJobPaymentCommand(CurrentUserId(), payeeUserId, collectionClientId, publicNote, internalNote, title));
        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error);
            await PopulatePayeesAsync();
            ViewBag.SelectedPayeeUserId = payeeUserId;
            ViewBag.SelectedCollectionClientId = collectionClientId;
            return View();
        }
        TempData["SuccessMessage"] = "Job payment created successfully.";
        _logger.LogInformation("Job payment {JobPaymentId} created by {ActorId}", result.Value!.Id, CurrentUserId());
        return RedirectToAction(nameof(Details), new { id = result.Value!.Id });
    }

    [HttpGet("{id:guid}/edit")]
    public async Task<IActionResult> Edit(Guid id)
    {
        var job = await _db.JobPayments.SingleOrDefaultAsync(j => j.Id == id);
        if (job == null || job.Status != JobPaymentStatus.Processing) return BadRequest("Only Processing job payments can be edited.");
        return View(job);
    }

    [HttpPost("{id:guid}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, [FromForm] string? title, [FromForm] string? publicNote, [FromForm] string? internalNote)
    {
        var result = await _service.UpdateMetadataAsync(new UpdateJobPaymentMetadataCommand(CurrentUserId(), id, title, publicNote, internalNote));
        return RedirectResult(result, id);
    }

    [HttpPost("{id:guid}/submit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(Guid id) => RedirectResult(await _service.SubmitAsync(id, CurrentUserId()), id);

    [HttpPost("{id:guid}/schedule")]
    [Authorize(Policy = PolicyNames.RequireAccountant)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Schedule(Guid id, [FromForm] DateTime scheduledAtUtc)
    {
        var result = await _service.ScheduleAsync(id, CurrentUserId(), DateTime.SpecifyKind(scheduledAtUtc, DateTimeKind.Utc));
        return RedirectResult(result, id);
    }

    [HttpPost("{id:guid}/mark-paid")]
    [Authorize(Policy = PolicyNames.RequireAccountant)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkPaid(Guid id, [FromForm] DateTime paymentDateUtc, [FromForm] string transactionNumber)
    {
        var result = await _service.MarkPaidAsync(id, CurrentUserId(), DateTime.SpecifyKind(paymentDateUtc, DateTimeKind.Utc), transactionNumber);
        return RedirectResult(result, id);
    }

    [HttpGet("adjustments/create")]
    [Authorize(Policy = PolicyNames.RequireAccountant)]
    public async Task<IActionResult> CreateAdjustment(Guid originalId)
    {
        var original = await _db.JobPayments.AsNoTracking().SingleOrDefaultAsync(j => j.Id == originalId);
        if (original == null || original.Status != JobPaymentStatus.Paid || original.IsAdjustment)
            return BadRequest("Adjustments must link to an original paid job payment.");
        ViewBag.Original = original;
        return View();
    }

    [HttpPost("adjustments/create")]
    [Authorize(Policy = PolicyNames.RequireAccountant)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateAdjustment([FromForm] Guid originalJobPaymentId, [FromForm] decimal amount, [FromForm] string reason)
    {
        var result = await _service.CreateAdjustmentAsync(new CreateJobPaymentAdjustmentCommand(CurrentUserId(), originalJobPaymentId, amount, reason));
        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error);
            ViewBag.Original = await _db.JobPayments.AsNoTracking().SingleOrDefaultAsync(j => j.Id == originalJobPaymentId);
            return View();
        }
        TempData["SuccessMessage"] = "Adjustment created and submitted.";
        return RedirectToAction(nameof(Details), new { id = result.Value!.Id });
    }

    [HttpPost("{id:guid}/approve-adjustment")]
    [Authorize(Policy = PolicyNames.RequireAdministrator)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveAdjustment(Guid id)
    {
        var result = await _service.ApproveAdjustmentAsync(id, CurrentUserId());
        return RedirectResult(result, id);
    }

    [HttpGet("{id:guid}")] public async Task<IActionResult> Details(Guid id) { var job = await QueryJob().SingleOrDefaultAsync(j => j.Id == id); return job is null ? NotFound() : View(job); }

    private async Task PopulatePayeesAsync()
    {
        ViewBag.Users = await _db.Users.AsNoTracking().Where(u => u.IsActive).OrderBy(u => u.FullName).ToListAsync();
        ViewBag.Clients = await _db.CollectionClients.AsNoTracking().Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync();
    }
    [HttpGet("{id:guid}/collections")] public async Task<IActionResult> Collections(Guid id) { var job = await _db.JobPayments.AsNoTracking().SingleOrDefaultAsync(j => j.Id == id); if (job?.CollectionClientId is null) return NotFound(); ViewBag.Job = job; return View(await _db.CollectionTransactions.AsNoTracking().Include(c => c.PurposeOption).Where(c => c.CollectionClientId == job.CollectionClientId && c.Status == CollectionStatus.Collected).ToListAsync()); }
    [HttpGet("{id:guid}/claims")]
    public async Task<IActionResult> Claims(Guid id)
    {
        var job = await _db.JobPayments.AsNoTracking().SingleOrDefaultAsync(j => j.Id == id);
        if (job?.PayeeUserId is null) return NotFound();
        ViewBag.Job = job;
        var eligibleClaims = await _db.Claims.AsNoTracking()
            .Where(c => c.ClaimantUserId == job.PayeeUserId && c.Status == ClaimStatus.Accepted && c.PaymentStatus == ClaimPaymentStatus.Unpaid && !c.IsDeleted)
            .OrderByDescending(c => c.CreatedAtUtc)
            .ToListAsync();
        return View(eligibleClaims);
    }

    [HttpPost("{id:guid}/claims/{claimId:guid}")][ValidateAntiForgeryToken] public async Task<IActionResult> AttachClaim(Guid id, Guid claimId) => RedirectResult(await _service.AttachClaimAsync(new(CurrentUserId(), id, claimId)), id);
    [HttpPost("{id:guid}/claims")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AttachClaims(Guid id, [FromForm] Guid[] claimIds) =>
        RedirectResult(await _service.AttachClaimsAsync(new(CurrentUserId(), id, claimIds)), id);
    [HttpPost("{id:guid}/claims/{claimId:guid}/remove")][ValidateAntiForgeryToken] public async Task<IActionResult> RemoveClaim(Guid id, Guid claimId) => RedirectResult(await _service.RemoveClaimAsync(new(CurrentUserId(), id, claimId)), id);

    [HttpPost("{id:guid}/collections/{collectionId:guid}")][ValidateAntiForgeryToken] public async Task<IActionResult> AttachCollection(Guid id, Guid collectionId) => RedirectResult(await _service.AttachCollectionAsync(new(CurrentUserId(), id, collectionId)), id);
    [HttpPost("{id:guid}/collections")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AttachCollections(Guid id, [FromForm] Guid[] collectionIds) =>
        RedirectResult(await _service.AttachCollectionsAsync(new(CurrentUserId(), id, collectionIds)), id);
    [HttpPost("{id:guid}/collections/{collectionId:guid}/remove")][ValidateAntiForgeryToken] public async Task<IActionResult> RemoveCollection(Guid id, Guid collectionId) => RedirectResult(await _service.RemoveCollectionAsync(new(CurrentUserId(), id, collectionId)), id);

    [HttpPost("{id:guid}/deductions")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddDeduction(Guid id, [FromForm] string description, [FromForm] decimal amount)
    {
        var result = await _service.AddDeductionAsync(new AddJobPaymentDeductionCommand(CurrentUserId(), id, description, amount));
        return RedirectResult(result, id);
    }
    [HttpPost("{id:guid}/resend")][ValidateAntiForgeryToken] public async Task<IActionResult> Resend(Guid id) => RedirectResult(await _service.ResendNotificationAsync(id, CurrentUserId()), id);
    [HttpGet("{id:guid}/print")] public async Task<IActionResult> Print(Guid id) { var job = await QueryJob().SingleOrDefaultAsync(j => j.Id == id); return job is null ? NotFound() : View(job); }
    private IQueryable<JobPayment> QueryJob() => _db.JobPayments.AsNoTracking()
        .Include(j => j.PayeeUser)
        .Include(j => j.CollectionClient)
        .Include(j => j.OriginalJobPayment)
        .Include(j => j.Claims).ThenInclude(x => x.Claim)
        .Include(j => j.Collections).ThenInclude(x => x.CollectionTransaction)
        .Include(j => j.Payrolls).ThenInclude(x => x.Payroll).ThenInclude(p => p.Entries)
        .Include(j => j.Deductions);
    private IActionResult RedirectResult(ElixomClaim.Lib.Common.Result result, Guid id) { TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] = result.IsSuccess ? "Job payment updated." : result.Error; return RedirectToAction(nameof(Details), new { id }); }
    private Guid CurrentUserId() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("UserId"), out var id) ? id : Guid.Empty;
}
