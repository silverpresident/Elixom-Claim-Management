using System.Security.Claims;
using ElixomClaim.Lib.Data;
using ElixomClaim.Lib.Entities;
using ElixomClaim.Lib.Services;
using ElixomClaim.Web.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using SecurityClaim = System.Security.Claims.Claim;
using DomainClaim = ElixomClaim.Lib.Entities.Claim;

namespace ElixomClaim.Web.Tests.Controllers;

public class Sprint10WorkflowsIntegrationTests
{
    private static ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task HomeDashboard_UserRole_ReturnsCorrectQueueCounts()
    {
        var db = CreateInMemoryDbContext();
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, Email = "user@elixom.com", FullName = "User 1", Role = UserRole.User, IsActive = true };
        db.Users.Add(user);

        // Seed 2 claims for user
        db.Claims.Add(new DomainClaim { ClaimantUserId = userId, Title = "C1", Description = "D1", Amount = 100m, Status = ClaimStatus.Draft });
        db.Claims.Add(new DomainClaim { ClaimantUserId = userId, Title = "C2", Description = "D2", Amount = 200m, Status = ClaimStatus.Submitted });
        await db.SaveChangesAsync();

        var controller = new HomeController(db, NullLogger<HomeController>.Instance);
        var claimsUser = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new SecurityClaim(ClaimTypes.NameIdentifier, userId.ToString()),
            new SecurityClaim(ClaimTypes.Role, UserRole.User.ToString())
        }, "TestAuth"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsUser }
        };

        var result = await controller.Index();
        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<HomeDashboardViewModel>(viewResult.Model);

        Assert.Equal(2, model.UserClaimsCount);
        Assert.Equal(1, model.PendingClaimsCount);
        Assert.Equal(user.Id, model.CurrentUser?.Id);
    }

    [Fact]
    public async Task RolePermissions_UnauthenticatedOrUnauthorizedAccess_DeniesOrRedacts()
    {
        var db = CreateInMemoryDbContext();
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, Email = "user@elixom.com", Role = UserRole.User, IsActive = true, BankAccountNumber = "9988776655" };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var summary = UserProfileSummary.FromUser(user, includeFullBankDetails: false);
        Assert.Equal("******6655", summary.BankAccountNumber);

        var fullSummary = UserProfileSummary.FromUser(user, includeFullBankDetails: true);
        Assert.Equal("9988776655", fullSummary.BankAccountNumber);
    }
}
