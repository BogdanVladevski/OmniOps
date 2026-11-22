using Microsoft.Extensions.Logging;
using OmniOps.Core.Interfaces;

namespace OmniOps.Infrastructure.Services;

/// <summary>Default integration sink: records each committed event in the log.</summary>
public class LoggingIntegrationEventPublisher : IIntegrationEventPublisher
{
    private readonly ILogger<LoggingIntegrationEventPublisher> _logger;

    public LoggingIntegrationEventPublisher(ILogger<LoggingIntegrationEventPublisher> logger)
    {
        _logger = logger;
    }

    public Task PublishAsync(
        string eventType,
        string payload,
        IReadOnlyDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Integration event {EventType} published ({Bytes} bytes)", eventType, payload.Length);
        return Task.CompletedTask;
    }
}
