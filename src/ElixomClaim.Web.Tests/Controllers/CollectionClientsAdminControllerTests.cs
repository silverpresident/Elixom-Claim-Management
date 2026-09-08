using ElixomClaim.Lib.Data;
using ElixomClaim.Lib.Entities;
using ElixomClaim.Lib.Services;
using ElixomClaim.Web.Controllers;
using ElixomClaim.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ElixomClaim.Web.Tests.Controllers;

public class CollectionClientsAdminControllerTests
{
    [Fact]
    public async Task Details_ProvidesOnlyActiveUnassignedUsersInSearchPicker()
    {
        await using var db = CreateDb();
        var client = new CollectionClient { Name = "Water Board" };
        var assigned = new User { Email = "assigned@example.test", NormalizedEmail = "ASSIGNED@EXAMPLE.TEST", FullName = "Assigned User" };
        var available = new User { Email = "available@example.test", NormalizedEmail = "AVAILABLE@EXAMPLE.TEST", FullName = "Available User", DisplayName = "Ava" };
        var inactive = new User { Email = "inactive@example.test", NormalizedEmail = "INACTIVE@EXAMPLE.TEST", FullName = "Inactive User", IsActive = false };
        db.AddRange(client, assigned, available, inactive);
        await db.SaveChangesAsync();
        db.CollectionClientUsers.Add(new CollectionClientUser { CollectionClientId = client.Id, UserId = assigned.Id });
        await db.SaveChangesAsync();

        var controller = new CollectionClientsAdminController(db, CreateService(db), NullLogger<CollectionClientsAdminController>.Instance);

        var result = await controller.Details(client.Id);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<CollectionClientDetailsViewModel>(view.Model);
        var candidate = Assert.Single(model.AvailableUsers);
        Assert.Equal(available.Id, candidate.Id);
        Assert.Equal("Ava", candidate.Name);
        Assert.Equal("available@example.test", candidate.Email);
    }

    private static ApplicationDbContext CreateDb() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options);

    private static CollectionClientAdministrationService CreateService(ApplicationDbContext db) => new(
        db,
        new AuditService(db, NullLogger<AuditService>.Instance),
        new SystemClock(),
        NullLogger<CollectionClientAdministrationService>.Instance);
}
