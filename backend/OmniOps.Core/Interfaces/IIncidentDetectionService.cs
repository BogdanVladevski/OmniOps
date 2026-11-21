using OmniOps.Core.Entities;

namespace OmniOps.Core.Interfaces;

public interface IIncidentDetectionService
{
    /// <summary>
    /// Evaluates the packet against detection rules without mutating detector state
    /// (cooldowns, previous-reading snapshot). Call <see cref="IncidentDetectionResult.CommitAsync"/>
    /// only after the packet and its incidents are durably persisted, so a failed save
    /// followed by a retry re-detects the same incidents instead of hitting a cooldown.
    /// </summary>
    Task<IncidentDetectionResult> DetectAsync(
        VehicleTelemetry telemetry,
        CancellationToken cancellationToken = default);
}

public sealed class IncidentDetectionResult
{
    public static readonly IncidentDetectionResult Empty = new([], _ => Task.CompletedTask);

    private readonly Func<CancellationToken, Task> _commit;

    public IncidentDetectionResult(
        IReadOnlyList<Incident> incidents,
        Func<CancellationToken, Task> commit)
    {
        Incidents = incidents;
        _commit = commit;
    }

    public IReadOnlyList<Incident> Incidents { get; }

    /// <summary>Applies the deferred detector state changes (cooldowns, previous-reading snapshot).</summary>
    public Task CommitAsync(CancellationToken cancellationToken = default) => _commit(cancellationToken);
}
