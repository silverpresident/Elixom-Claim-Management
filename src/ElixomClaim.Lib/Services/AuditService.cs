using System.Text.Json;
using System.Text.Json.Nodes;
using ElixomClaim.Lib.Data;
using ElixomClaim.Lib.Entities;
using Microsoft.Extensions.Logging;

namespace ElixomClaim.Lib.Services;

public class AuditService : IAuditService
{
    private static readonly HashSet<string> SensitiveKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "secret", "token", "clientsecret", "refresh_token", "access_token",
        "bankaccountnumber", "bankbranchcode", "accountnumber", "creditcard", "cvv", "pin",
        "ssn", "body", "emailbody", "htmlbody", "textbody"
    };

    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<AuditService> _logger;

    public AuditService(ApplicationDbContext dbContext, ILogger<AuditService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task LogAsync(
        string action,
        string target,
        object? beforeState = null,
        object? afterState = null,
        string? actorUserId = null,
        string? actorEmail = null,
        string? correlationId = null,
        string? ipAddress = null,
        bool isMcpOperation = false,
        CancellationToken cancellationToken = default)
    {
        var beforeJson = SerializeAndRedact(beforeState);
        var afterJson = SerializeAndRedact(afterState);

        var record = new AuditRecord
        {
            Action = action,
            Target = target,
            BeforeStateJson = beforeJson,
            AfterStateJson = afterJson,
            ActorUserId = actorUserId,
            ActorEmail = actorEmail,
            CorrelationId = correlationId,
            IpAddress = ipAddress,
            IsMcpOperation = isMcpOperation,
            TimestampUtc = DateTime.UtcNow
        };

        _dbContext.AuditRecords.Add(record);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Audit event recorded: {Action} on {Target} by {Actor} [Correlation: {CorrelationId}, MCP: {IsMcp}]",
            action, target, actorEmail ?? actorUserId ?? "System", correlationId ?? "N/A", isMcpOperation);
    }

    public string RedactJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return json;
        }

        try
        {
            var node = JsonNode.Parse(json);
            if (node == null)
            {
                return json;
            }

            RedactNode(node);
            return node.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
        }
        catch
        {
            return "[REDACTED_UNPARSABLE]";
        }
    }

    private string? SerializeAndRedact(object? obj)
    {
        if (obj == null)
        {
            return null;
        }

        if (obj is string str)
        {
            return RedactJson(str);
        }

        try
        {
            var json = JsonSerializer.Serialize(obj);
            return RedactJson(json);
        }
        catch
        {
            return "[REDACTED_UNSERIALIZABLE]";
        }
    }

    private static void RedactNode(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            var keys = obj.Select(kvp => kvp.Key).ToList();
            foreach (var key in keys)
            {
                if (IsSensitiveKey(key))
                {
                    obj[key] = "[REDACTED]";
                }
                else if (obj[key] is JsonNode childNode)
                {
                    RedactNode(childNode);
                }
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var item in array)
            {
                if (item != null)
                {
                    RedactNode(item);
                }
            }
        }
    }

    private static bool IsSensitiveKey(string key)
    {
        if (SensitiveKeys.Contains(key))
        {
            return true;
        }

        return SensitiveKeys.Any(s => key.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0);
    }
}
