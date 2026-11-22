using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OmniOps.Application.Common.Interfaces;

namespace OmniOps.Infrastructure.Email;

/// <summary>
/// Plain-text email through any SMTP provider (Microsoft 365, Google Workspace, Brevo, SES...). Without a server
/// configured it only logs, and reports <see cref="IsConfigured"/> = false so nothing claims an email was sent.
/// </summary>
internal sealed class SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    private readonly EmailOptions _options = options.Value;

    public bool IsConfigured => _options.IsConfigured;

    public async Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            logger.LogInformation("Email not sent (no mail server configured) to {To}: {Subject}", to, subject);
            return;
        }

        using var message = new MailMessage { Subject = subject, Body = body, IsBodyHtml = false };
        message.From = new MailAddress(_options.From!);
        message.To.Add(new MailAddress(to));
        using var client = new SmtpClient(_options.SmtpHost, _options.SmtpPort) { EnableSsl = _options.EnableSsl };
        if (!string.IsNullOrWhiteSpace(_options.Username))
        {
            client.Credentials = new NetworkCredential(_options.Username, _options.Password);
        }

        await client.SendMailAsync(message, cancellationToken);
    }
}

internal sealed class PublicLinks(IOptions<PublicUrlOptions> options) : IPublicLinks
{
    private readonly string _api = options.Value.ApiUrl.TrimEnd('/');
    private readonly string _app = options.Value.AppUrl.TrimEnd('/');

    public string Invoice(string token) => $"{_api}/i/{token}";

    public string Tracking(string token) => $"{_api}/t/{token}";

    public string Join(string token) => $"{_app}/join/{Uri.EscapeDataString(token)}";

    public string PasswordReset(string token) => $"{_app}/reset/{Uri.EscapeDataString(token)}";
}
