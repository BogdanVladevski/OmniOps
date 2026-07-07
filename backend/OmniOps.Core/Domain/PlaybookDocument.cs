using OmniOps.Core.Enums;

namespace OmniOps.Core.Domain;

/// <summary>
/// A single cold-chain incident-response playbook (an SOP markdown file under docs/playbooks/),
/// parsed into its frontmatter tags plus the procedural body used for RAG narrative generation.
/// </summary>
public class PlaybookDocument
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public AnomalySeverity? Severity { get; init; }
    public IReadOnlyList<string> ProductCategories { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> IncidentTypes { get; init; } = Array.Empty<string>();
    public int MinExcursionSeconds { get; init; }

    /// <summary>The procedural body (markdown minus frontmatter), passed to the LLM as retrieved context.</summary>
    public string Body { get; init; } = string.Empty;
}
