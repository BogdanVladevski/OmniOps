namespace OmniOps.Core.Interfaces;

/// <summary>
/// Runs the incident playbook when anomaly detection fires.
/// Current impl is a stub — swap in LangGraph, Semantic Kernel, etc. when ready.
/// </summary>
public interface IPlaybookOrchestrationService
{
    Task OrchestrateIncidentResponseAsync(
        string vehicleId,
        string anomalySummary,
        CancellationToken cancellationToken = default);
}
