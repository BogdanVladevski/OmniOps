using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OmniOps.Core.Interfaces;
using OmniOps.Infrastructure.Configuration;

namespace OmniOps.Infrastructure.Services;

public class EventReplayService : IEventReplayService
{
    private readonly IEventStore _eventStore;
    private readonly IKafkaMessageProducer _producer;
    private readonly KafkaOptions _kafkaOptions;
    private readonly ILogger<EventReplayService> _logger;

    public EventReplayService(
        IEventStore eventStore,
        IKafkaMessageProducer producer,
        IOptions<KafkaOptions> kafkaOptions,
        ILogger<EventReplayService> logger)
    {
        _eventStore = eventStore;
        _producer = producer;
        _kafkaOptions = kafkaOptions.Value;
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
            await _producer.ProduceAsync(
                _kafkaOptions.EventsTopic,
                stored.Payload,
                new Dictionary<string, string>
                {
                    ["event-type"] = stored.EventType,
                    ["schema-version"] = stored.SchemaVersion.ToString(),
                    ["replay"] = "true"
                },
                cancellationToken);
        }

        _logger.LogInformation(
            "Replayed {Count} events from {FromUtc} to {ToUtc} to topic {Topic}",
            events.Count, fromUtc, toUtc, _kafkaOptions.EventsTopic);

        return events.Count;
    }
}
