using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Testcontainers.Kafka;

namespace OmniOps.Infrastructure.Tests.Kafka;

public class KafkaConnectivityTests : IAsyncLifetime
{
    private readonly KafkaContainer _kafka = new KafkaBuilder()
        .WithImage("confluentinc/cp-kafka:7.6.0")
        .Build();

    public async Task InitializeAsync() => await _kafka.StartAsync();

    public async Task DisposeAsync() => await _kafka.DisposeAsync();

    [Fact]
    public async Task CanProduceAndConsumeOnEphemeralTopic()
    {
        const string topic = "fleet-telemetry-test";
        var bootstrap = _kafka.GetBootstrapAddress();

        using var admin = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = bootstrap }).Build();
        await admin.CreateTopicsAsync([
            new TopicSpecification { Name = topic, NumPartitions = 1, ReplicationFactor = 1 }
        ]);

        using var producer = new ProducerBuilder<Null, string>(
            new ProducerConfig { BootstrapServers = bootstrap }).Build();

        await producer.ProduceAsync(topic, new Message<Null, string> { Value = "ping" });

        using var consumer = new ConsumerBuilder<Ignore, string>(new ConsumerConfig
        {
            BootstrapServers = bootstrap,
            GroupId = $"test-{Guid.NewGuid():N}",
            AutoOffsetReset = AutoOffsetReset.Earliest
        }).Build();

        consumer.Subscribe(topic);
        var result = consumer.Consume(TimeSpan.FromSeconds(10));

        Assert.NotNull(result);
        Assert.Equal("ping", result!.Message.Value);
    }
}
