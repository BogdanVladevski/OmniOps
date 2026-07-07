using OmniOps.Core.Domain;
using OmniOps.Core.Enums;
using OmniOps.Core.Interfaces;

namespace OmniOps.Infrastructure.Services.Rag;

/// <summary>
/// Scores each playbook against the incident by product category, severity, excursion duration,
/// and incident-type keywords, and returns the top matches. Deterministic and dependency-free —
/// appropriate for a corpus this size. Always returns at least one doc when the corpus is non-empty
/// (the lowest-scoring general fallback), so callers never have to handle an empty retrieval.
/// </summary>
public class KeywordPlaybookRetrievalService : IPlaybookRetrievalService
{
    private readonly PlaybookLoader _loader;

    public KeywordPlaybookRetrievalService(PlaybookLoader loader)
    {
        _loader = loader;
    }

    public IReadOnlyList<PlaybookDocument> Retrieve(IncidentContext incident, int maxResults = 2)
    {
        var corpus = _loader.GetAll();
        if (corpus.Count == 0)
        {
            return Array.Empty<PlaybookDocument>();
        }

        var productCategory = MapProductCategory(incident.ProductName);
        var incidentKeywords = DeriveIncidentKeywords(incident);

        var scored = corpus
            .Select(doc => new { Doc = doc, Score = Score(doc, incident, productCategory, incidentKeywords) })
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Doc.Id, StringComparer.Ordinal)
            .ToList();

        // Keep only positively-scored matches, but guarantee at least one result as a fallback.
        var matches = scored.Where(x => x.Score > 0).Select(x => x.Doc).ToList();
        if (matches.Count == 0)
        {
            return new[] { scored[0].Doc };
        }

        return matches.Take(Math.Max(1, maxResults)).ToList();
    }

    private static int Score(
        PlaybookDocument doc,
        IncidentContext incident,
        string productCategory,
        IReadOnlyCollection<string> incidentKeywords)
    {
        var score = 0;

        if (doc.ProductCategories.Contains(productCategory))
        {
            score += 3;
        }

        if (doc.Severity == incident.Severity)
        {
            score += 2;
        }

        // Duration-gated docs (e.g. prolonged breach) only qualify once the excursion is long enough.
        if (doc.MinExcursionSeconds > 0)
        {
            if (incident.ExcursionDurationSeconds >= doc.MinExcursionSeconds)
            {
                score += 2;
            }
            else
            {
                score -= 2;
            }
        }

        if (doc.IncidentTypes.Any(incidentKeywords.Contains))
        {
            score += 1;
        }

        return score;
    }

    private static string MapProductCategory(string? productName)
    {
        if (string.IsNullOrWhiteSpace(productName))
        {
            return "general";
        }

        var name = productName.ToLowerInvariant();

        if (name.Contains("insulin") || name.Contains("glargine") || name.Contains("aspart"))
        {
            return "insulin";
        }

        if (name.Contains("vaccine") || name.Contains("bcg") || name.Contains("hepatitis"))
        {
            return "vaccine";
        }

        return "biologic";
    }

    private static IReadOnlyCollection<string> DeriveIncidentKeywords(IncidentContext incident)
    {
        var keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (incident.Severity == AnomalySeverity.Warning)
        {
            keywords.Add("trend");
            keywords.Add("early-warning");
            keywords.Add("in-range-drift");
        }

        if (incident.ExcursionDurationSeconds >= 60)
        {
            keywords.Add("prolonged-excursion");
            keywords.Add("sustained-breach");
        }

        if (incident.ExcursionDurationSeconds > 0)
        {
            keywords.Add("excursion");
            keywords.Add("warm-excursion");
        }

        return keywords;
    }
}
