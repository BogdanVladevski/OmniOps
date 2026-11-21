namespace OmniOps.Core.Interfaces;

/// <summary>
/// Hands committed domain events (from the outbox) to whatever integrations are configured
/// — today a log sink; later customer webhooks. Implementations must be safe to call repeatedly
/// with the same event: the outbox delivers at least once.
/// </summary>
public interface IIntegrationEventPublisher
{
    Task PublishAsync(
        string eventType,
        string payload,
        IReadOnlyDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default);
}
