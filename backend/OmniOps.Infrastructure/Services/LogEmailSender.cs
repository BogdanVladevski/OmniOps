using Microsoft.Extensions.Logging;
using OmniOps.Core.Interfaces;

namespace OmniOps.Infrastructure.Services;

public class LogEmailSender : IEmailSender
{
    private readonly ILogger<LogEmailSender> _logger;

    public LogEmailSender(ILogger<LogEmailSender> logger) => _logger = logger;

    public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Email to {To}: {Subject} — {BodyPreview}", to, subject, body[..Math.Min(body.Length, 120)]);
        return Task.CompletedTask;
    }
}
