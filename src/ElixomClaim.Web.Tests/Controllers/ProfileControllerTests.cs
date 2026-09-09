using System.Security.Claims;
using ElixomClaim.Lib.Data;
using ElixomClaim.Lib.Entities;
using ElixomClaim.Lib.Services;
using ElixomClaim.Web.Controllers;
using ElixomClaim.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using SecurityClaim = System.Security.Claims.Claim;

namespace ElixomClaim.Web.Tests.Controllers;

public class ProfileControllerTests
{
    private static ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task Index_ActiveUser_ReturnsViewWithMaskedBankDetails()
    {
        var db = CreateInMemoryDbContext();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "user1@elixom.com",
            FullName = "Jane Doe",
            Role = UserRole.User,
            IsActive = true,
            BankName = "National Bank",
            BankAccountName = "Jane Doe",
            BankAccountNumber = "1234567890",
            BankBranchCode = "001"
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var audit = new AuditService(db, NullLogger<AuditService>.Instance);
        var controller = new ProfileController(db, audit, NullLogger<ProfileController>.Instance);

        var claimsUser = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new SecurityClaim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new SecurityClaim(ClaimTypes.Role, UserRole.User.ToString())
        }, "TestAuth"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsUser }
        };

        var result = await controller.Index();
        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<User>(viewResult.Model);

        Assert.Equal(user.Id, model.Id);
        var summary = (UserProfileSummary)controller.ViewBag.Summary;
        Assert.NotNull(summary);
        Assert.Equal("******7890", summary.BankAccountNumber);
    }

    private class TestTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    [Fact]
    public async Task UpdateBankDetails_ValidData_UpdatesUserAndLogsAuditWithRedactedAccountNumber()
    {
        var db = CreateInMemoryDbContext();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "user1@elixom.com",
            FullName = "Jane Doe",
            Role = UserRole.User,
            IsActive = true,
            BankName = "Old Bank",
            BankAccountName = "Jane Doe",
            BankAccountNumber = "11111111",
            BankBranchCode = "000"
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var audit = new AuditService(db, NullLogger<AuditService>.Instance);
        var httpContext = new DefaultHttpContext();
        var controller = new ProfileController(db, audit, NullLogger<ProfileController>.Instance)
        {
            TempData = new TempDataDictionary(httpContext, new TestTempDataProvider())
        };

        var claimsUser = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new SecurityClaim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new SecurityClaim(ClaimTypes.Role, UserRole.User.ToString())
        }, "TestAuth"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsUser }
        };

        var input = new ProfileController.UpdateBankDetailsInput(
            BankAccountName: "Jane Doe Updated",
            BankAccountNumber: "987654321",
            BankName: "First National Bank",
            BankBranchCode: "123",
            BankBranchName: "Downtown Kingston",
            BankAccountType: "Savings"
        );

        var result = await controller.UpdateBankDetails(input);
        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirectResult.ActionName);

        var updatedUser = await db.Users.FindAsync(user.Id);
        Assert.NotNull(updatedUser);
        Assert.Equal("First National Bank", updatedUser.BankName);
        Assert.Equal("Jane Doe Updated", updatedUser.BankAccountName);
        Assert.Equal("987654321", updatedUser.BankAccountNumber);
        Assert.Equal("123", updatedUser.BankBranchCode);
        Assert.Equal("Downtown Kingston", updatedUser.BankBranchName);
        Assert.Equal("Savings", updatedUser.BankAccountType);

        var auditLog = await db.AuditRecords.FirstOrDefaultAsync(a => a.Action == "USER_BANK_DETAILS_UPDATED");
        Assert.NotNull(auditLog);
        Assert.Equal("User", auditLog.EntityType);
        Assert.Equal(user.Id.ToString(), auditLog.EntityId);
        Assert.NotNull(auditLog.AfterStateJson);
        Assert.Contains("\"BankAccountNumber\":\"[REDACTED]\"", auditLog.AfterStateJson);
        Assert.DoesNotContain("987654321", auditLog.AfterStateJson);
    }

    [Fact]
    public async Task UpdateDisplayName_ActiveUser_UpdatesOnlyDisplayNameAndAuditsChange()
    {
        var db = CreateInMemoryDbContext();
        var user = new User { Id = Guid.NewGuid(), Email = "user1@elixom.com", FullName = "Jane Doe", Role = UserRole.User, IsActive = true };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var httpContext = new DefaultHttpContext();
        var controller = new ProfileController(db, new AuditService(db, NullLogger<AuditService>.Instance), NullLogger<ProfileController>.Instance)
        {
            TempData = new TempDataDictionary(httpContext, new TestTempDataProvider()),
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new SecurityClaim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                        new SecurityClaim(ClaimTypes.Role, UserRole.User.ToString())
                    }, "TestAuth"))
                }
            }
        };

        var result = await controller.UpdateDisplayName(new ProfileController.UpdateDisplayNameInput("  Jay  "));

        Assert.Equal("Index", Assert.IsType<RedirectToActionResult>(result).ActionName);
        Assert.Equal("Jay", (await db.Users.FindAsync(user.Id))!.DisplayName);
        var auditLog = await db.AuditRecords.SingleAsync(a => a.Action == "USER_DISPLAY_NAME_UPDATED");
        Assert.Contains("Jay", auditLog.AfterStateJson);
    }
}
