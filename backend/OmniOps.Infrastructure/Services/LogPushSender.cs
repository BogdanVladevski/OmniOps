using Microsoft.Extensions.Logging;
using OmniOps.Core.Interfaces;

namespace OmniOps.Infrastructure.Services;

public class LogPushSender : IPushSender
{
    private readonly ILogger<LogPushSender> _logger;

    public LogPushSender(ILogger<LogPushSender> logger) => _logger = logger;

    public Task SendAsync(string pushToken, string title, string body, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Push to {TokenPrefix}: {Title}", pushToken[..Math.Min(12, pushToken.Length)], title);
        return Task.CompletedTask;
    }
}
