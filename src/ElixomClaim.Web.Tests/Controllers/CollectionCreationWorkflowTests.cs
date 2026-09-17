using ElixomClaim.Lib.Configuration;
using ElixomClaim.Lib.Data;
using ElixomClaim.Lib.Entities;
using ElixomClaim.Lib.Services;
using ElixomClaim.Web.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ElixomClaim.Web.Tests.Controllers;

public sealed class CollectionCreationWorkflowTests
{
    [Fact]
    public async Task Create_ShowsOnlyTheClientSelectionStep()
    {
        await using var db = CreateDb();
        db.CollectionClients.AddRange(
            new CollectionClient { Name = "Active client" },
            new CollectionClient { Name = "Inactive client", IsActive = false });
        await db.SaveChangesAsync();
        var controller = CreateController(db);

        var result = await controller.Create();

        var view = Assert.IsType<ViewResult>(result);
        Assert.IsType<SelectCollectionClientInput>(view.Model);
        var clients = Assert.IsAssignableFrom<IEnumerable<CollectionClient>>(view.ViewData["Clients"]);
        Assert.Collection(clients, client => Assert.Equal("Active client", client.Name));
    }

    [Fact]
    public async Task SelectClient_RequiresAnActiveClientAndRedirectsToTheTransactionStep()
    {
        await using var db = CreateDb();
        var activeClient = new CollectionClient { Name = "Active client" };
        db.CollectionClients.AddRange(activeClient, new CollectionClient { Name = "Inactive client", IsActive = false });
        await db.SaveChangesAsync();
        var controller = CreateController(db);

        var invalid = await controller.SelectClient(new SelectCollectionClientInput());
        var invalidView = Assert.IsType<ViewResult>(invalid);
        Assert.Equal("Create", invalidView.ViewName);
        Assert.False(controller.ModelState.IsValid);

        controller.ModelState.Clear();
        var valid = await controller.SelectClient(new SelectCollectionClientInput { CollectionClientId = activeClient.Id });

        var redirect = Assert.IsType<RedirectToActionResult>(valid);
        Assert.Equal(nameof(CollectionsController.CreateTransaction), redirect.ActionName);
        Assert.Equal(activeClient.Id, redirect.RouteValues!["clientId"]);
    }

    [Fact]
    public async Task CreateTransaction_RequiresAClientAndScopesSuggestionsToIt()
    {
        await using var db = CreateDb();
        var selected = new CollectionClient { Name = "Selected" };
        var other = new CollectionClient { Name = "Other" };
        db.CollectionClients.AddRange(selected, other);
        db.CollectionPurposeOptions.AddRange(
            new CollectionPurposeOption { CollectionClient = selected, Name = "Selected purpose" },
            new CollectionPurposeOption { CollectionClient = other, Name = "Other purpose" });
        db.CollectionAmountOptions.AddRange(
            new CollectionAmountOption { CollectionClient = selected, Name = "Selected amount", Amount = 10m },
            new CollectionAmountOption { CollectionClient = other, Name = "Other amount", Amount = 20m });
        await db.SaveChangesAsync();
        var controller = CreateController(db);

        var direct = await controller.CreateTransaction(null);
        var redirect = Assert.IsType<RedirectToActionResult>(direct);
        Assert.Equal(nameof(CollectionsController.Create), redirect.ActionName);

        var result = await controller.CreateTransaction(selected.Id);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("Transaction", view.ViewName);
        var model = Assert.IsType<RecordCollectionInput>(view.Model);
        Assert.Equal(selected.Id, model.CollectionClientId);
        Assert.Collection(Assert.IsAssignableFrom<IEnumerable<CollectionPurposeOption>>(view.ViewData["Purposes"]), option => Assert.Equal("Selected purpose", option.Name));
        Assert.Collection(Assert.IsAssignableFrom<IEnumerable<CollectionAmountOption>>(view.ViewData["Amounts"]), option => Assert.Equal("Selected amount", option.Name));
    }

    private static ApplicationDbContext CreateDb() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static CollectionsController CreateController(ApplicationDbContext db)
    {
        var service = new CollectionService(db, new AuditService(db, NullLogger<AuditService>.Instance), new SystemClock(), Options.Create(new NotificationOptions()), NullLogger<CollectionService>.Instance);
        return new CollectionsController(db, service, NullLogger<CollectionsController>.Instance)
        {
            TempData = new TempDataDictionary(new DefaultHttpContext(), new TestTempDataProvider())
        };
    }

    private sealed class TestTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
