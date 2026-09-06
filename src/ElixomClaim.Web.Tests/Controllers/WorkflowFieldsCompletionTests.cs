using System.Security.Claims;
using ElixomClaim.Lib.Data;
using ElixomClaim.Lib.Entities;
using ElixomClaim.Lib.Services;
using ElixomClaim.Web.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using SecurityClaim = System.Security.Claims.Claim;

namespace ElixomClaim.Web.Tests.Controllers;

public class WorkflowFieldsCompletionTests
{
    private static ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private class TestTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    [Fact]
    public async Task CreateClaim_WithDateOfJob_SavesAndExposesDateOfJob()
    {
        var db = CreateInMemoryDbContext();
        var userId = Guid.NewGuid();
        var claimService = new ClaimService(db, new AuditService(db, NullLogger<AuditService>.Instance), NullLogger<ClaimService>.Instance);
        var controller = new ClaimsController(claimService, db);

        var claimsUser = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new SecurityClaim(ClaimTypes.NameIdentifier, userId.ToString()),
            new SecurityClaim(ClaimTypes.Role, UserRole.User.ToString())
        }, "TestAuth"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsUser }
        };

        var jobDate = new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc);
        var input = new ClaimsController.CreateClaimInput("Taxi Reimbursement", "Travel to client site", 1500m, DateOfJob: jobDate);

        var result = await controller.Create(input);
        var redirect = Assert.IsType<RedirectToActionResult>(result);

        var claim = await db.Claims.FirstOrDefaultAsync(c => c.ClaimantUserId == userId);
        Assert.NotNull(claim);
        Assert.Equal("Taxi Reimbursement", claim.Title);
        Assert.Equal(jobDate, claim.DateOfJob);
    }

    [Fact]
    public async Task UpdateCollectionClient_Admin_UpdatesDescriptionNotesAndFees()
    {
        var db = CreateInMemoryDbContext();
        var adminId = Guid.NewGuid();
        var clock = new SystemClock();

        var adminUser = new User { Id = adminId, Email = "admin@elixom.com", Role = UserRole.Administrator, IsActive = true };
        db.Users.Add(adminUser);
        await db.SaveChangesAsync();

        var adminService = new CollectionClientAdministrationService(db, new AuditService(db, NullLogger<AuditService>.Instance), clock, NullLogger<CollectionClientAdministrationService>.Instance);

        var createResult = await adminService.CreateClientAsync(new CreateCollectionClientCommand(adminId, "Power Utility"));
        Assert.True(createResult.IsSuccess, createResult.Error);
        var client = createResult.Value!;

        var controller = new CollectionClientsAdminController(db, adminService, NullLogger<CollectionClientsAdminController>.Instance)
        {
            TempData = new TempDataDictionary(new DefaultHttpContext(), new TestTempDataProvider())
        };

        var claimsUser = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new SecurityClaim(ClaimTypes.NameIdentifier, adminId.ToString()),
            new SecurityClaim(ClaimTypes.Role, UserRole.Administrator.ToString())
        }, "TestAuth"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsUser }
        };

        var updateResult = await controller.Update(
            client.Id,
            name: "Power Utility Corp",
            description: "Public electricity payments",
            notes: "Internal admin note",
            perJobProcessingFee: 25.00m,
            perTransactionFee: 2.50m);

        var redirect = Assert.IsType<RedirectToActionResult>(updateResult);

        var updatedClient = await db.CollectionClients.FindAsync(client.Id);
        Assert.NotNull(updatedClient);
        Assert.Equal("Power Utility Corp", updatedClient.Name);
        Assert.Equal("Public electricity payments", updatedClient.Description);
        Assert.Equal("Internal admin note", updatedClient.Notes);
        Assert.Equal(25.00m, updatedClient.PerJobProcessingFee);
        Assert.Equal(2.50m, updatedClient.PerTransactionFee);
    }

    [Fact]
    public async Task RecordCollection_WithPayorTelephone_StoresTelephone()
    {
        var db = CreateInMemoryDbContext();
        var tellerId = Guid.NewGuid();
        var clock = new SystemClock();
        var notificationOpts = Microsoft.Extensions.Options.Options.Create(new Lib.Configuration.NotificationOptions());

        var teller = new User { Id = tellerId, Email = "teller@elixom.com", Role = UserRole.Teller, IsActive = true };
        var adminUser = new User { Id = Guid.NewGuid(), Email = "admin2@elixom.com", Role = UserRole.Administrator, IsActive = true };
        db.Users.AddRange(teller, adminUser);
        await db.SaveChangesAsync();

        var adminService = new CollectionClientAdministrationService(db, new AuditService(db, NullLogger<AuditService>.Instance), clock, NullLogger<CollectionClientAdministrationService>.Instance);

        var client = (await adminService.CreateClientAsync(new CreateCollectionClientCommand(adminUser.Id, "Water Board"))).Value!;
        var purpose = (await adminService.AddPurposeOptionAsync(new AddCollectionPurposeOptionCommand(adminUser.Id, client.Id, "Water Bill", 1))).Value!;
        var amountOpt = (await adminService.AddAmountOptionAsync(new AddCollectionAmountOptionCommand(adminUser.Id, client.Id, "Standard Bill", 5000m, 1))).Value!;

        var collectionService = new CollectionService(db, new AuditService(db, NullLogger<AuditService>.Instance), clock, notificationOpts, NullLogger<CollectionService>.Instance);
        var controller = new CollectionsController(db, collectionService, NullLogger<CollectionsController>.Instance)
        {
            TempData = new TempDataDictionary(new DefaultHttpContext(), new TestTempDataProvider())
        };

        var claimsUser = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new SecurityClaim(ClaimTypes.NameIdentifier, tellerId.ToString()),
            new SecurityClaim(ClaimTypes.Role, UserRole.Teller.ToString())
        }, "TestAuth"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsUser }
        };

        var input = new RecordCollectionInput
        {
            CollectionClientId = client.Id,
            PurposeOptionId = purpose.Id,
            AmountOptionId = amountOpt.Id,
            PayorName = "Robert Smith",
            PayorEmail = "robert@example.com",
            PayorTelephone = "876-555-0199",
            Method = CollectionMethod.Cash,
            ProcessingFee = 50m,
            PaymentDateUtc = DateTime.UtcNow
        };

        var result = await controller.Create(input);
        var redirect = Assert.IsType<RedirectToActionResult>(result);

        var record = await db.CollectionTransactions.FirstOrDefaultAsync(c => c.PayorName == "Robert Smith");
        Assert.NotNull(record);
        Assert.Equal("876-555-0199", record.PayorTelephone);
        Assert.Equal(50m, record.ProcessingFee);
    }
}
