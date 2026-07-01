namespace OmniOps.Core.Interfaces;

public interface IKafkaMessageProducer
{
    Task ProduceAsync(
        string topic,
        string payload,
        IReadOnlyDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default);
}
