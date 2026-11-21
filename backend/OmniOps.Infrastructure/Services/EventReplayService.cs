using Microsoft.Extensions.Logging;
using OmniOps.Core.Interfaces;

namespace OmniOps.Infrastructure.Services;

public class EventReplayService : IEventReplayService
{
    private readonly IEventStore _eventStore;
    private readonly IIntegrationEventPublisher _publisher;
    private readonly ILogger<EventReplayService> _logger;

    public EventReplayService(
        IEventStore eventStore,
        IIntegrationEventPublisher publisher,
        ILogger<EventReplayService> logger)
    {
        _eventStore = eventStore;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task<int> ReplayAsync(
        DateTime fromUtc,
        DateTime toUtc,
        string? eventType = null,
        CancellationToken cancellationToken = default)
    {
        var events = await _eventStore.GetByTimeRangeAsync(fromUtc, toUtc, eventType, cancellationToken);

        foreach (var stored in events)
        {
            await _publisher.PublishAsync(
                stored.EventType,
                stored.Payload,
                new Dictionary<string, string>
                {
                    ["schema-version"] = stored.SchemaVersion.ToString(),
                    ["replay"] = "true"
                },
                cancellationToken);
        }

        _logger.LogInformation(
            "Replayed {Count} events from {FromUtc} to {ToUtc}",
            events.Count, fromUtc, toUtc);

        return events.Count;
    }
}
