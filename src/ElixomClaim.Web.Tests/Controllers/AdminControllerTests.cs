using System.Security.Claims;
using ElixomClaim.Lib.Data;
using ElixomClaim.Lib.Entities;
using ElixomClaim.Lib.Services;
using ElixomClaim.Web.Controllers;
using ElixomClaim.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ElixomClaim.Web.Tests.Controllers;

public class AdminControllerTests
{
    private static ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task AuditLogs_ManagerRole_HidesStateDataAndSensitiveDetails()
    {
        var db = CreateInMemoryDbContext();
        var audit = new AuditService(db, NullLogger<AuditService>.Instance);

        // Seed audit log containing sensitive email body and bank account
        await audit.LogAsync(
            action: "EMAIL_SENT",
            target: "Claim:100",
            afterState: new { recipient = "john@example.com", emailBody = "Secret Body Content", bankAccountNumber = "999888777" },
            actorEmail: "system@elixom.com",
            correlationId: "corr-100");

        var controller = new AdminController(db, audit);
        var managerUser = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "mgr-1"),
            new Claim(ClaimTypes.Role, UserRole.Manager.ToString())
        }, "TestAuth"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = managerUser }
        };

        var result = await controller.AuditLogs();
        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<IEnumerable<AuditRecordViewModel>>(viewResult.Model);

        var log = Assert.Single(model);
        Assert.Null(log.BeforeStateJson);
        Assert.Null(log.AfterStateJson);
        Assert.Equal("EMAIL_SENT", log.Action);
    }

    [Fact]
    public async Task AuditLogs_AdministratorRole_IncludesRedactedStateData()
    {
        var db = CreateInMemoryDbContext();
        var audit = new AuditService(db, NullLogger<AuditService>.Instance);

        await audit.LogAsync(
            action: "CLAIM_UPDATED",
            target: "Claim:100",
            afterState: new { claimId = 100, bankAccountNumber = "12345678" },
            actorEmail: "admin@elixom.com",
            correlationId: "corr-101");

        var controller = new AdminController(db, audit);
        var adminUser = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "admin-1"),
            new Claim(ClaimTypes.Role, UserRole.Administrator.ToString())
        }, "TestAuth"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = adminUser }
        };

        var result = await controller.AuditLogs();
        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<IEnumerable<AuditRecordViewModel>>(viewResult.Model);

        var log = Assert.Single(model);
        Assert.NotNull(log.AfterStateJson);
        Assert.Contains("\"bankAccountNumber\":\"[REDACTED]\"", log.AfterStateJson);
    }
}
