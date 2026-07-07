using OmniOps.Core.Domain;

namespace OmniOps.Core.Interfaces;

/// <summary>
/// Lightweight keyword/tag retrieval over the docs/playbooks/ corpus. Picks the most relevant
/// 1–2 SOPs for an incident by scoring product category, severity, excursion duration, and
/// incident-type tags. No vector DB — the corpus is small and tag-scoped retrieval is sufficient.
/// </summary>
public interface IPlaybookRetrievalService
{
    IReadOnlyList<PlaybookDocument> Retrieve(IncidentContext incident, int maxResults = 2);
}
