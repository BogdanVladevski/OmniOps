using OmniOps.Core.Domain;

namespace OmniOps.Core.Interfaces;

/// <summary>
/// Runs the incident playbook when anomaly detection fires: retrieves the relevant SOPs for the
/// incident and generates a specific response narrative (LLM-backed with a templated fallback),
/// then broadcasts it over the existing SignalR path.
/// </summary>
public interface IPlaybookOrchestrationService
{
    Task OrchestrateIncidentResponseAsync(
        IncidentContext incident,
        CancellationToken cancellationToken = default);
}
