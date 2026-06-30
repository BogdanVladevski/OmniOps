namespace OmniOps.Infrastructure.Configuration
{
    public class KafkaOptions
    {
        public const string SectionName = "Kafka";
        public string BootstrapServers { get; set; } = "127.0.0.1:9092";
        public string Topic { get; set; } = "fleet-telemetry";
        public string GroupId { get; set; } = "omniops-consumer-group-v2";
    }
}
