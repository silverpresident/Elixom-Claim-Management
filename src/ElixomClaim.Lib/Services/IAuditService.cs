namespace ElixomClaim.Lib.Services;

public sealed record AuditEntity(string EntityType, string EntityId)
{
    public static AuditEntity ParseLegacyTarget(string target)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(target);
        var parts = target.Split(':', 2);
        return new AuditEntity(parts[0].Trim(), parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[1]) ? parts[1].Trim() : target.Trim());
    }
}

public interface IAuditService
{
    Task LogAsync(
        string action,
        AuditEntity entity,
        object? beforeState = null,
        object? afterState = null,
        string? actorUserId = null,
        string? actorEmail = null,
        string? correlationId = null,
        string? ipAddress = null,
        bool isMcpOperation = false,
        CancellationToken cancellationToken = default);

    [Obsolete("Use the AuditEntity overload.")]
    Task LogAsync(
        string action,
        string target,
        object? beforeState = null,
        object? afterState = null,
        string? actorUserId = null,
        string? actorEmail = null,
        string? correlationId = null,
        string? ipAddress = null,
        bool isMcpOperation = false,
        CancellationToken cancellationToken = default);

    string RedactJson(string json);
}
