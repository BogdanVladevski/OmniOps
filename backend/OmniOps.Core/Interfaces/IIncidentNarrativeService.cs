using OmniOps.Core.Domain;

namespace OmniOps.Core.Interfaces;

/// <summary>
/// Generates a specific incident-response narrative from the incident facts and the retrieved
/// playbook text. Implementations must never throw into the caller: on LLM failure/timeout they
/// return a clearly-labeled templated narrative built directly from the retrieved playbooks.
/// </summary>
public interface IIncidentNarrativeService
{
    Task<string> GenerateNarrativeAsync(
        IncidentContext incident,
        IReadOnlyList<PlaybookDocument> playbooks,
        CancellationToken cancellationToken = default);
}
