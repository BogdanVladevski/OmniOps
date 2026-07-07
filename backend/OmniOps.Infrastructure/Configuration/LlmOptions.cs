namespace OmniOps.Infrastructure.Configuration;

public class LlmOptions
{
    public const string SectionName = "Llm";

    /// <summary>Master switch. When false, narratives always use the templated fallback (no external call).</summary>
    public bool Enabled { get; set; }

    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gpt-4o-mini";

    /// <summary>OpenAI-compatible chat completions base URL. Override for Azure/OpenRouter/local gateways.</summary>
    public string BaseUrl { get; set; } = "https://api.openai.com/v1";

    public int TimeoutSeconds { get; set; } = 12;

    /// <summary>Playbook corpus directory, resolved relative to the content root when not absolute.</summary>
    public string PlaybooksDirectory { get; set; } = "docs/playbooks";

    public bool IsConfigured => Enabled && !string.IsNullOrWhiteSpace(ApiKey);
}
