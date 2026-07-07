using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using OmniOps.Core.Domain;
using OmniOps.Core.Enums;

namespace OmniOps.Infrastructure.Services.Rag;

/// <summary>
/// Loads and parses the docs/playbooks/*.md corpus once and caches it. Each file is expected to
/// carry a simple YAML-style frontmatter block delimited by --- lines followed by the markdown body.
/// </summary>
public class PlaybookLoader
{
    private readonly ILogger<PlaybookLoader> _logger;
    private readonly Lazy<IReadOnlyList<PlaybookDocument>> _documents;

    public PlaybookLoader(string playbooksDirectory, ILogger<PlaybookLoader> logger)
    {
        _logger = logger;
        _documents = new Lazy<IReadOnlyList<PlaybookDocument>>(() => Load(playbooksDirectory));
    }

    public IReadOnlyList<PlaybookDocument> GetAll() => _documents.Value;

    private IReadOnlyList<PlaybookDocument> Load(string directory)
    {
        var resolved = Path.IsPathRooted(directory)
            ? directory
            : Path.GetFullPath(directory, Directory.GetCurrentDirectory());

        if (!Directory.Exists(resolved))
        {
            // Content-root relative fallback (dotnet run cwd differs from repo root in some setups).
            var fallback = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, directory));
            if (Directory.Exists(fallback))
            {
                resolved = fallback;
            }
        }

        if (!Directory.Exists(resolved))
        {
            _logger.LogWarning(
                "Playbook directory not found at {Directory}; RAG retrieval will have an empty corpus",
                resolved);
            return Array.Empty<PlaybookDocument>();
        }

        var docs = new ConcurrentBag<PlaybookDocument>();

        foreach (var file in Directory.EnumerateFiles(resolved, "*.md"))
        {
            try
            {
                docs.Add(Parse(File.ReadAllText(file)));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse playbook {File}", file);
            }
        }

        var result = docs.OrderBy(d => d.Id).ToList();
        _logger.LogInformation("Loaded {Count} incident playbooks from {Directory}", result.Count, resolved);
        return result;
    }

    public static PlaybookDocument Parse(string content)
    {
        var (frontmatter, body) = SplitFrontmatter(content);

        return new PlaybookDocument
        {
            Id = frontmatter.GetValueOrDefault("id", string.Empty),
            Title = frontmatter.GetValueOrDefault("title", string.Empty),
            Severity = ParseSeverity(frontmatter.GetValueOrDefault("severity")),
            ProductCategories = ParseList(frontmatter.GetValueOrDefault("product_categories")),
            IncidentTypes = ParseList(frontmatter.GetValueOrDefault("incident_types")),
            MinExcursionSeconds = ParseInt(frontmatter.GetValueOrDefault("min_excursion_seconds")),
            Body = body.Trim()
        };
    }

    private static (Dictionary<string, string> Frontmatter, string Body) SplitFrontmatter(string content)
    {
        var frontmatter = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var normalized = content.Replace("\r\n", "\n").TrimStart();

        if (!normalized.StartsWith("---\n", StringComparison.Ordinal))
        {
            return (frontmatter, content);
        }

        var end = normalized.IndexOf("\n---", 4, StringComparison.Ordinal);
        if (end < 0)
        {
            return (frontmatter, content);
        }

        var block = normalized.Substring(4, end - 4);
        var body = normalized[(end + 4)..];

        foreach (var line in block.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = line.IndexOf(':');
            if (separator <= 0)
            {
                continue;
            }

            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();
            frontmatter[key] = value;
        }

        return (frontmatter, body);
    }

    private static IReadOnlyList<string> ParseList(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Array.Empty<string>();
        }

        return raw.Trim().TrimStart('[').TrimEnd(']')
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(v => v.Trim('"', '\'').ToLowerInvariant())
            .Where(v => v.Length > 0)
            .ToList();
    }

    private static AnomalySeverity? ParseSeverity(string? raw) =>
        Enum.TryParse<AnomalySeverity>(raw, ignoreCase: true, out var severity) ? severity : null;

    private static int ParseInt(string? raw) =>
        int.TryParse(raw, out var value) ? value : 0;
}
