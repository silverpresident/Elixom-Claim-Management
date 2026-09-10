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
using SecurityClaim = System.Security.Claims.Claim;

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

        var controller = new AdminController(db, audit, NullLogger<AdminController>.Instance);
        var managerUser = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new SecurityClaim(ClaimTypes.NameIdentifier, "mgr-1"),
            new SecurityClaim(ClaimTypes.Role, UserRole.Manager.ToString())
        }, "TestAuth"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = managerUser }
        };

        var result = await controller.AuditLogs();
        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<AuditLogPageViewModel>(viewResult.Model).Records;

        var log = Assert.Single(model);
        Assert.Null(log.BeforeStateJson);
        Assert.Null(log.AfterStateJson);
        Assert.Equal("EMAIL_SENT", log.Action);
    }

    [Fact]
    public async Task AuditLogs_ManagerRole_OnlySeesApprovedOperationalDomains()
    {
        var db = CreateInMemoryDbContext();
        var audit = new AuditService(db, NullLogger<AuditService>.Instance);
        await audit.LogAsync("CLAIM_UPDATED", new AuditEntity("Claim", "100"));
        await audit.LogAsync("PAYROLL_GENERATED", new AuditEntity("Payroll", "200"));
        await audit.LogAsync("OAUTH_TOKEN_REVOKED", new AuditEntity("OAuthClient", "client-1"));

        var controller = new AdminController(db, audit, NullLogger<AdminController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new SecurityClaim(ClaimTypes.Role, UserRole.Manager.ToString()) }, "TestAuth"))
                }
            }
        };

        var model = Assert.IsType<AuditLogPageViewModel>(Assert.IsType<ViewResult>(await controller.AuditLogs()).Model).Records;
        var log = Assert.Single(model);
        Assert.Equal("Claim", log.EntityType);
        Assert.Null(log.BeforeStateJson);
        Assert.Null(log.AfterStateJson);
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

        var controller = new AdminController(db, audit, NullLogger<AdminController>.Instance);
        var adminUser = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new SecurityClaim(ClaimTypes.NameIdentifier, "admin-1"),
            new SecurityClaim(ClaimTypes.Role, UserRole.Administrator.ToString())
        }, "TestAuth"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = adminUser }
        };

        var result = await controller.AuditLogs();
        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<AuditLogPageViewModel>(viewResult.Model).Records;

        var log = Assert.Single(model);
        Assert.NotNull(log.AfterStateJson);
        Assert.Contains("\"bankAccountNumber\":\"[REDACTED]\"", log.AfterStateJson);
    }

    [Fact]
    public async Task AuditLogs_ManagerRole_PaginatesOnlyPermittedMetadata()
    {
        var db = CreateInMemoryDbContext();
        var audit = new AuditService(db, NullLogger<AuditService>.Instance);
        for (var index = 0; index < 51; index++)
        {
            await audit.LogAsync("CLAIM_UPDATED", new AuditEntity("Claim", index.ToString()), afterState: new { bankAccountNumber = "999888777" });
        }
        await audit.LogAsync("PAYROLL_GENERATED", new AuditEntity("Payroll", "excluded"));

        var controller = new AdminController(db, audit, NullLogger<AdminController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new SecurityClaim(ClaimTypes.Role, UserRole.Manager.ToString()) }, "TestAuth")) } }
        };

        var page = Assert.IsType<AuditLogPageViewModel>(Assert.IsType<ViewResult>(await controller.AuditLogs(page: 2)).Model);

        Assert.Equal(2, page.Page);
        Assert.Equal(51, page.TotalCount);
        var record = Assert.Single(page.Records);
        Assert.Equal("Claim", record.EntityType);
        Assert.Null(record.AfterStateJson);
    }

    [Fact]
    public async Task EmailLogs_Administrator_CanReviewBoundedDeliveryRecordsAndDetails()
    {
        var db = CreateInMemoryDbContext();
        var audit = new AuditService(db, NullLogger<AuditService>.Instance);
        var emailLog = new EmailLog
        {
            Id = Guid.NewGuid(),
            To = "recipient@example.com",
            Subject = "Payment summary",
            HtmlBody = "<p>Encoded review only</p>",
            Provider = "Development",
            Status = EmailOutboxStatus.Sent,
            RelatedEntityType = "JobPayment",
            RelatedEntityId = "42"
        };
        db.EmailLogs.Add(emailLog);
        await db.SaveChangesAsync();

        var controller = new AdminController(db, audit, NullLogger<AdminController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new SecurityClaim(ClaimTypes.Role, UserRole.Administrator.ToString()) }, "TestAuth"))
                }
            }
        };

        var list = Assert.IsType<ViewResult>(await controller.EmailLogs());
        Assert.Single(Assert.IsAssignableFrom<IEnumerable<EmailLog>>(list.Model));
        var detail = Assert.IsType<ViewResult>(await controller.EmailLog(emailLog.Id));
        Assert.Equal(emailLog.Id, Assert.IsType<EmailLog>(detail.Model).Id);
    }
}
