namespace OmniOps.Core.Interfaces;

public interface IEventReplayService
{
    /// <summary>
    /// Re-publishes stored events in the requested window to the Kafka events topic without mutating aggregates.
    /// </summary>
    Task<int> ReplayAsync(
        DateTime fromUtc,
        DateTime toUtc,
        string? eventType = null,
        CancellationToken cancellationToken = default);
}
