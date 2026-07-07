using System.Text;
using OmniOps.Core.Domain;

namespace OmniOps.Infrastructure.Services.Rag;

/// <summary>
/// Builds the system + user prompt for incident-narrative generation, and the templated fallback
/// used when the LLM is disabled or unavailable. Kept separate so the prompt design is testable
/// and the fallback shares the exact same retrieved-playbook context as the LLM path.
/// </summary>
public static class IncidentNarrativePromptBuilder
{
    public const string SystemPrompt =
        "You are a cold-chain quality-assurance officer for a pharmaceutical logistics operator. " +
        "Write a short, specific incident-response note (4–7 sentences) for the on-call operator. " +
        "Ground every recommended action in the provided Standard Operating Procedures, citing their " +
        "SOP ids. Reference the concrete shipment facts (product, batch, temperature, duration, value at " +
        "risk). Be direct and operational — no generic filler, no invented facts beyond what is provided.";

    public static string BuildUserPrompt(IncidentContext incident, IReadOnlyList<PlaybookDocument> playbooks)
    {
        var builder = new StringBuilder();

        builder.AppendLine("INCIDENT FACTS");
        builder.AppendLine($"- Vehicle: {incident.VehicleId}");
        builder.AppendLine($"- Severity: {incident.Severity}");
        builder.AppendLine($"- Cargo temperature: {incident.TemperatureCelsius:0.#} C");

        if (incident.MinSafeTempCelsius.HasValue && incident.MaxSafeTempCelsius.HasValue)
        {
            builder.AppendLine(
                $"- Safe range: {incident.MinSafeTempCelsius:0.#}–{incident.MaxSafeTempCelsius:0.#} C");
        }

        builder.AppendLine($"- Excursion duration: {incident.ExcursionDurationSeconds}s");

        if (!string.IsNullOrWhiteSpace(incident.ProductName))
        {
            builder.AppendLine($"- Product: {incident.ProductName}");
        }

        if (!string.IsNullOrWhiteSpace(incident.BatchNumber))
        {
            builder.AppendLine($"- Batch: {incident.BatchNumber}");
        }

        if (incident.ValueAtRiskUsd.HasValue)
        {
            builder.AppendLine($"- Estimated value at risk: ${incident.ValueAtRiskUsd:N0}");
        }

        builder.AppendLine();
        builder.AppendLine("RELEVANT STANDARD OPERATING PROCEDURES");

        foreach (var playbook in playbooks)
        {
            builder.AppendLine();
            builder.AppendLine($"### {playbook.Id} — {playbook.Title}");
            builder.AppendLine(playbook.Body);
        }

        builder.AppendLine();
        builder.AppendLine(
            "Write the incident-response note now, applying the SOP steps above to these specific facts.");

        return builder.ToString();
    }

    /// <summary>
    /// Deterministic narrative used when the LLM is disabled or the call fails. Clearly labelled so
    /// operators know it is not model-generated, and built from the same retrieved SOP context.
    /// </summary>
    public static string BuildFallbackNarrative(
        IncidentContext incident,
        IReadOnlyList<PlaybookDocument> playbooks)
    {
        var builder = new StringBuilder();
        builder.AppendLine("[Auto-generated from SOP — AI narrative unavailable]");
        builder.AppendLine(incident.IncidentSummary);

        if (playbooks.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Applicable procedures:");
            foreach (var playbook in playbooks)
            {
                builder.AppendLine($"- {playbook.Id}: {playbook.Title}");
            }
        }

        return builder.ToString().TrimEnd();
    }
}
