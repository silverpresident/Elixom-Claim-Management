using ElixomClaim.Lib.Authorization;
using ElixomClaim.Lib.Data;
using ElixomClaim.Lib.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ElixomClaim.Web.Controllers;

[Authorize(Policy = PolicyNames.RequireManager)]
[Route("manager/collections")]
public sealed class ManagerCollectionsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<ManagerCollectionsController> _logger;

    public ManagerCollectionsController(ApplicationDbContext db, ILogger<ManagerCollectionsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] Guid? collectionClientId, CancellationToken cancellationToken)
    {
        var clients = await _db.CollectionClients.AsNoTracking()
            .Where(client => client.IsActive)
            .OrderBy(client => client.Name)
            .ToListAsync(cancellationToken);

        if (collectionClientId.HasValue && clients.All(client => client.Id != collectionClientId.Value))
        {
            _logger.LogWarning("Manager collection queue requested an unavailable client {CollectionClientId}.", collectionClientId);
            return NotFound();
        }

        var collectionsQuery = _db.CollectionTransactions.AsNoTracking()
            .Include(collection => collection.CollectionClient)
            .AsQueryable();
        if (collectionClientId.HasValue)
        {
            collectionsQuery = collectionsQuery.Where(collection => collection.CollectionClientId == collectionClientId.Value);
        }

        IEnumerable<JobPayment> processingJobs = collectionClientId.HasValue
            ? await _db.JobPayments.AsNoTracking()
                .Where(job => job.CollectionClientId == collectionClientId.Value && job.Status == JobPaymentStatus.Processing)
                .OrderByDescending(job => job.CreatedAtUtc)
                .ToListAsync(cancellationToken)
            : Array.Empty<JobPayment>();

        ViewBag.Clients = clients;
        ViewBag.SelectedCollectionClientId = collectionClientId;
        ViewBag.ProcessingJobs = processingJobs;

        return View(await collectionsQuery
            .OrderByDescending(collection => collection.PaymentDateUtc)
            .ToListAsync(cancellationToken));
    }
}
