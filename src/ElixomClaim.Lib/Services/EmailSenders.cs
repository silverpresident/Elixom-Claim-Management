using System.Net;
using System.Net.Mail;
using Azure;
using Azure.Communication.Email;
using ElixomClaim.Lib.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ElixomClaim.Lib.Services;

public class SmtpEmailSender : IEmailSender
{
    private readonly NotificationOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;
    public SmtpEmailSender(IOptions<NotificationOptions> options, ILogger<SmtpEmailSender> logger) { _options = options.Value; _logger = logger; }
    public string ProviderName => "Smtp";

    public async Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.SmtpHost)) return new(false, "SMTP host is not configured.");
        try
        {
            using var client = new SmtpClient(_options.SmtpHost, _options.SmtpPort) { EnableSsl = _options.SmtpUseSsl };
            if (!string.IsNullOrWhiteSpace(_options.SmtpUserName)) client.Credentials = new NetworkCredential(_options.SmtpUserName, _options.SmtpPassword);
            using var mail = new MailMessage(_options.FromAddress, message.Recipient, message.Subject, message.HtmlBody) { IsBodyHtml = true };
            await client.SendMailAsync(mail, cancellationToken);
            return new(true);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "SMTP delivery failed for queued email.");
            return new(false, "SMTP delivery failed.");
        }
    }
}

public class AcsEmailSender : IEmailSender
{
    private readonly NotificationOptions _options;
    private readonly ILogger<AcsEmailSender> _logger;
    public AcsEmailSender(IOptions<NotificationOptions> options, ILogger<AcsEmailSender> logger) { _options = options.Value; _logger = logger; }
    public string ProviderName => "Acs";
    public async Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.AcsConnectionString)) return new(false, "ACS connection string is not configured.");
        try
        {
            var client = new EmailClient(_options.AcsConnectionString);
            var content = new EmailContent(message.Subject) { Html = message.HtmlBody };
            var email = new Azure.Communication.Email.EmailMessage(_options.FromAddress, message.Recipient, content);
            var operation = await client.SendAsync(WaitUntil.Completed, email, cancellationToken);
            return operation.Value.Status == EmailSendStatus.Succeeded ? new(true) : new(false, "ACS delivery did not succeed.");
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "ACS delivery failed for queued email.");
            return new(false, "ACS delivery failed.");
        }
    }
}

public class FakeEmailSender : IEmailSender
{
    public string ProviderName => "Fake";
    public List<EmailMessage> SentMessages { get; } = [];
    public Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default) { SentMessages.Add(message); return Task.FromResult(new EmailSendResult(true)); }
}
