using System.Text.Json;
using ElixomClaim.Lib.Data;
using ElixomClaim.Lib.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ElixomClaim.Lib.Tests.Services;

public class AuditServiceTests
{
    private static ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public void RedactJson_RedactsSensitiveFields()
    {
        var db = CreateInMemoryDbContext();
        var service = new AuditService(db, NullLogger<AuditService>.Instance);

        var input = JsonSerializer.Serialize(new
        {
            user = "jdoe",
            password = "SecretPassword123!",
            bankAccountNumber = "1234567890",
            emailBody = "<p>Confidential Message</p>",
            nested = new
            {
                clientSecret = "super-secret-key",
                normalField = "normal-value"
            }
        });

        var redactedJson = service.RedactJson(input);

        Assert.Contains("\"password\":\"[REDACTED]\"", redactedJson);
        Assert.Contains("\"bankAccountNumber\":\"[REDACTED]\"", redactedJson);
        Assert.Contains("\"emailBody\":\"[REDACTED]\"", redactedJson);
        Assert.Contains("\"clientSecret\":\"[REDACTED]\"", redactedJson);
        Assert.Contains("\"user\":\"jdoe\"", redactedJson);
        Assert.Contains("\"normalField\":\"normal-value\"", redactedJson);
    }

    [Fact]
    public async Task LogAsync_SavesRedactedAuditRecordToDatabase()
    {
        var db = CreateInMemoryDbContext();
        var service = new AuditService(db, NullLogger<AuditService>.Instance);

        var beforeState = new { user = "oldUser", password = "OldPassword123" };
        var afterState = new { user = "newUser", password = "NewPassword123" };

        await service.LogAsync(
            action: "USER_UPDATE",
            target: "User:123",
            beforeState: beforeState,
            afterState: afterState,
            actorUserId: "admin-1",
            actorEmail: "admin@elixom.com",
            correlationId: "corr-789",
            ipAddress: "127.0.0.1",
            isMcpOperation: true);

        var record = await db.AuditRecords.FirstOrDefaultAsync();
        Assert.NotNull(record);
        Assert.Equal("USER_UPDATE", record.Action);
        Assert.Equal("User:123", record.Target);
        Assert.Equal("admin-1", record.ActorUserId);
        Assert.Equal("admin@elixom.com", record.ActorEmail);
        Assert.Equal("corr-789", record.CorrelationId);
        Assert.Equal("127.0.0.1", record.IpAddress);
        Assert.True(record.IsMcpOperation);

        Assert.Contains("\"password\":\"[REDACTED]\"", record.BeforeStateJson);
        Assert.Contains("\"password\":\"[REDACTED]\"", record.AfterStateJson);
        Assert.Contains("\"user\":\"oldUser\"", record.BeforeStateJson);
        Assert.Contains("\"user\":\"newUser\"", record.AfterStateJson);
    }
}
