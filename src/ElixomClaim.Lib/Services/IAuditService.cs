namespace ElixomClaim.Lib.Services;

public interface IAuditService
{
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
