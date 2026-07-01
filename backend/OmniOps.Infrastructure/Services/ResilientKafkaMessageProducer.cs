using System.Text;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OmniOps.Core.Interfaces;
using Polly;

namespace OmniOps.Infrastructure.Services;

public class ResilientKafkaMessageProducer : IKafkaMessageProducer
{
    private readonly IProducer<Null, string> _producer;
    private readonly ResiliencePipeline _pipeline;
    private readonly ILogger<ResilientKafkaMessageProducer> _logger;

    public ResilientKafkaMessageProducer(
        IProducer<Null, string> producer,
        [FromKeyedServices(Resilience.KafkaResiliencePipeline.Name)] ResiliencePipeline pipeline,
        ILogger<ResilientKafkaMessageProducer> logger)
    {
        _producer = producer;
        _pipeline = pipeline;
        _logger = logger;
    }

    public async Task ProduceAsync(
        string topic,
        string payload,
        IReadOnlyDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        await _pipeline.ExecuteAsync(
            async token =>
            {
                var message = new Message<Null, string> { Value = payload };

                if (headers is not null && headers.Count > 0)
                {
                    message.Headers = new Headers();
                    foreach (var (key, value) in headers)
                    {
                        message.Headers.Add(key, Encoding.UTF8.GetBytes(value));
                    }
                }

                var result = await _producer.ProduceAsync(topic, message, token);

                if (result.Status != PersistenceStatus.Persisted)
                {
                    throw new InvalidOperationException(
                        $"Kafka produce to '{topic}' returned status {result.Status}");
                }

                _logger.LogDebug(
                    "Produced message to topic {Topic} partition {Partition} offset {Offset}",
                    topic, result.Partition, result.Offset);
            },
            cancellationToken);
    }
}
