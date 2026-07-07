using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OmniOps.Core.Domain;
using OmniOps.Core.Interfaces;
using OmniOps.Infrastructure.Configuration;

namespace OmniOps.Infrastructure.Services.Rag;

/// <summary>
/// Generates incident narratives via an OpenAI-compatible chat completions API. Never throws into
/// the caller: if the LLM is not configured, disabled, times out, or errors, it returns a clearly
/// labelled templated narrative built from the same retrieved playbooks.
/// </summary>
public class OpenAiIncidentNarrativeService : IIncidentNarrativeService
{
    private readonly HttpClient _httpClient;
    private readonly LlmOptions _options;
    private readonly ILogger<OpenAiIncidentNarrativeService> _logger;

    public OpenAiIncidentNarrativeService(
        HttpClient httpClient,
        IOptions<LlmOptions> options,
        ILogger<OpenAiIncidentNarrativeService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> GenerateNarrativeAsync(
        IncidentContext incident,
        IReadOnlyList<PlaybookDocument> playbooks,
        CancellationToken cancellationToken = default)
    {
        if (!_options.IsConfigured)
        {
            _logger.LogInformation(
                "LLM narrative disabled or unconfigured; using templated fallback for vehicle {VehicleId}",
                incident.VehicleId);
            return IncidentNarrativePromptBuilder.BuildFallbackNarrative(incident, playbooks);
        }

        try
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(_options.TimeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            var narrative = await CallLlmAsync(incident, playbooks, linkedCts.Token);

            if (string.IsNullOrWhiteSpace(narrative))
            {
                _logger.LogWarning(
                    "LLM returned an empty narrative for vehicle {VehicleId}; using templated fallback",
                    incident.VehicleId);
                return IncidentNarrativePromptBuilder.BuildFallbackNarrative(incident, playbooks);
            }

            return narrative.Trim();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "LLM narrative generation failed for vehicle {VehicleId}; using templated fallback",
                incident.VehicleId);
            return IncidentNarrativePromptBuilder.BuildFallbackNarrative(incident, playbooks);
        }
    }

    private async Task<string?> CallLlmAsync(
        IncidentContext incident,
        IReadOnlyList<PlaybookDocument> playbooks,
        CancellationToken cancellationToken)
    {
        var request = new ChatCompletionRequest
        {
            Model = _options.Model,
            Temperature = 0.3,
            Messages =
            [
                new ChatMessage { Role = "system", Content = IncidentNarrativePromptBuilder.SystemPrompt },
                new ChatMessage
                {
                    Role = "user",
                    Content = IncidentNarrativePromptBuilder.BuildUserPrompt(incident, playbooks)
                }
            ]
        };

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"{_options.BaseUrl.TrimEnd('/')}/chat/completions")
        {
            Content = JsonContent.Create(request)
        };
        httpRequest.Headers.Add("Authorization", $"Bearer {_options.ApiKey}");

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        response.EnsureSuccessStatusCode();

        var completion = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(
            cancellationToken: cancellationToken);

        return completion?.Choices?.FirstOrDefault()?.Message?.Content;
    }

    private sealed class ChatCompletionRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; init; } = string.Empty;

        [JsonPropertyName("temperature")]
        public double Temperature { get; init; }

        [JsonPropertyName("messages")]
        public IReadOnlyList<ChatMessage> Messages { get; init; } = Array.Empty<ChatMessage>();
    }

    private sealed class ChatMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; init; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; init; } = string.Empty;
    }

    private sealed class ChatCompletionResponse
    {
        [JsonPropertyName("choices")]
        public List<Choice>? Choices { get; init; }
    }

    private sealed class Choice
    {
        [JsonPropertyName("message")]
        public ChatMessage? Message { get; init; }
    }
}
