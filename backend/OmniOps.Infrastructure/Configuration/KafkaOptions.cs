namespace OmniOps.Infrastructure.Configuration;

public class KafkaOptions
{
    public const string SectionName = "Kafka";

    public string BootstrapServers { get; set; } = "127.0.0.1:9092";
    public string Topic { get; set; } = "fleet-telemetry";
    public string EventsTopic { get; set; } = "fleet-telemetry-events";
    public string DlqTopic { get; set; } = "fleet-telemetry-dlq";
    public string GroupId { get; set; } = "omniops-telemetry-group";

    /// <summary>Bounded in-process retries for transient processing failures before DLQ routing.</summary>
    public int ProcessingRetryMaxAttempts { get; set; } = 3;

    public int ProcessingRetryDelayMilliseconds { get; set; } = 500;
}
