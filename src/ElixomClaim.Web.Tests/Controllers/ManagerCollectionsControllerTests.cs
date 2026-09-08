using ElixomClaim.Lib.Data;
using ElixomClaim.Lib.Entities;
using ElixomClaim.Web.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ElixomClaim.Web.Tests.Controllers;

public sealed class ManagerCollectionsControllerTests
{
    [Fact]
    public async Task Index_FiltersCollectionsAndProcessingJobsToTheSelectedClient()
    {
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var selectedClient = new CollectionClient { Name = "Selected client" };
        var otherClient = new CollectionClient { Name = "Other client" };
        db.AddRange(selectedClient, otherClient);
        db.AddRange(
            new CollectionTransaction { CollectionClient = selectedClient, Purpose = "Selected", PayorName = "Payor", Amount = 25m, Status = CollectionStatus.Collected },
            new CollectionTransaction { CollectionClient = otherClient, Purpose = "Other", PayorName = "Payor", Amount = 50m, Status = CollectionStatus.Collected },
            new JobPayment { CollectionClient = selectedClient, Status = JobPaymentStatus.Processing },
            new JobPayment { CollectionClient = selectedClient, Status = JobPaymentStatus.Submitted });
        await db.SaveChangesAsync();
        var controller = new ManagerCollectionsController(db, NullLogger<ManagerCollectionsController>.Instance);

        var result = await controller.Index(selectedClient.Id, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var collections = Assert.IsAssignableFrom<IEnumerable<CollectionTransaction>>(view.Model);
        Assert.Collection(collections, item => Assert.Equal(selectedClient.Id, item.CollectionClientId));
        var jobs = Assert.IsAssignableFrom<IEnumerable<JobPayment>>(view.ViewData["ProcessingJobs"]);
        Assert.Collection(jobs, item => Assert.Equal(JobPaymentStatus.Processing, item.Status));
    }

    [Fact]
    public async Task Index_RejectsAnUnknownOrInactiveClientFilter()
    {
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.CollectionClients.Add(new CollectionClient { Name = "Inactive", IsActive = false });
        await db.SaveChangesAsync();
        var controller = new ManagerCollectionsController(db, NullLogger<ManagerCollectionsController>.Instance);

        var result = await controller.Index(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }
}
