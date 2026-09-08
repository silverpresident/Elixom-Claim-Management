using ElixomClaim.Lib.Common;

namespace ElixomClaim.Lib.Services;

public sealed record ApprovedEmailPreview(string Subject, string RedactedHtmlBody, IReadOnlyList<string> RecipientSummary);

public interface IApprovedEmailPreviewService
{
    Task<Result<ApprovedEmailPreview>> PreviewAsync(Guid actorUserId, string templateType, Guid entityId, CancellationToken cancellationToken = default);
}
