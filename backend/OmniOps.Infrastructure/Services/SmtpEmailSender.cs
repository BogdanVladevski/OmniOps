using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using OmniOps.Core.Interfaces;
using OmniOps.Infrastructure.Configuration;

namespace OmniOps.Infrastructure.Services;

/// <summary>Plain-text email through an SMTP server (any provider: Gmail/Workspace, Microsoft 365, Brevo, SES…).</summary>
public class SmtpEmailSender : IEmailSender
{
    private readonly EmailOptions _options;

    public SmtpEmailSender(IOptions<EmailOptions> options) => _options = options.Value;

    public bool IsConfigured => _options.IsConfigured;

    public async Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
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

public class PublicLinks : IPublicLinks
{
    private readonly string _base;

    public PublicLinks(IOptions<PublicUrlOptions> options) => _base = options.Value.BaseUrl.TrimEnd('/');

    public string Invoice(string token) => $"{_base}/i/{token}";

    public string Tracking(string token) => $"{_base}/t/{token}";
}
