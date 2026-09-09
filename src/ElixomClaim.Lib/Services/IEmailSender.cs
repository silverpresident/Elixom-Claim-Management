namespace ElixomClaim.Lib.Services;

public interface IEmailSender
{
    string ProviderName { get; }
    Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}

public record EmailMessage(string To, string From, string? Cc, string? Bcc, string Subject, string HtmlBody);
public record EmailSendResult(bool Succeeded, string? FailureReason = null);
