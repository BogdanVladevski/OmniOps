namespace OmniOps.Core.Interfaces;

/// <summary>
/// Modular abstraction for RAG-driven autonomous incident response.
/// Implementations integrate with LangGraph, Semantic Kernel, or similar agent frameworks.
/// </summary>
public interface IPlaybookOrchestrationService
{
    Task OrchestrateIncidentResponseAsync(
        string vehicleId,
        string anomalySummary,
        CancellationToken cancellationToken = default);
}
