using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OmniOps.Core.Interfaces;
using OmniOps.Infrastructure.Configuration;
using OmniOps.Infrastructure.Data;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace OmniOps.Infrastructure.Services.Rag;

public class FleetCopilotService : IFleetCopilotService
{
    private readonly AppDbContext _context;
    private readonly IFleetAnalyticsService _analytics;
    private readonly IPredictionService _predictions;
    private readonly LlmOptions _llmOptions;
    private readonly HttpClient _httpClient;
    private readonly ILogger<FleetCopilotService> _logger;

    public FleetCopilotService(
        AppDbContext context,
        IFleetAnalyticsService analytics,
        IPredictionService predictions,
        IOptions<LlmOptions> llmOptions,
        HttpClient httpClient,
        ILogger<FleetCopilotService> logger)
    {
        _context = context;
        _analytics = analytics;
        _predictions = predictions;
        _llmOptions = llmOptions.Value;
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<string> AskAsync(string question, Guid fleetId, CancellationToken cancellationToken = default)
    {
        var context = await BuildContextAsync(fleetId, cancellationToken);

        if (_llmOptions.Enabled && !string.IsNullOrWhiteSpace(_llmOptions.ApiKey))
        {
            try
            {
                return await AskLlmAsync(question, context, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Copilot LLM call failed; using templated fallback");
            }
        }

        return BuildTemplatedAnswer(question, context);
    }

    private async Task<string> BuildContextAsync(Guid fleetId, CancellationToken cancellationToken)
    {
        var to = DateTime.UtcNow;
        var from = to.AddHours(-24);
        var fleetStats = await _analytics.GetFleetAnalyticsAsync(fleetId, from, to, cancellationToken);
        var drivers = await _analytics.GetDriverAnalyticsAsync(fleetId, from, to, cancellationToken);
        var operational = await _analytics.GetOperationalAnalyticsAsync(fleetId, from, to, cancellationToken);
        var openIncidents = await _context.Incidents.AsNoTracking()
            .Where(i => i.FleetId == fleetId && i.Status == Core.Enums.IncidentStatus.Open)
            .OrderByDescending(i => i.DetectedAtUtc)
            .Take(5)
            .ToListAsync(cancellationToken);

        var sb = new StringBuilder();
        sb.AppendLine($"Fleet {fleetId}: {fleetStats.VehicleCount} vehicles, {fleetStats.ActiveVehicles} active.");
        sb.AppendLine($"24h: {fleetStats.TotalDistanceKm} km, avg speed {fleetStats.AvgSpeedKmh} km/h, {fleetStats.IncidentCount} incidents.");
        sb.AppendLine($"Operations: {operational.ActiveTrips} active trips, {operational.OpenIncidents} open incidents.");
        foreach (var d in drivers.Take(5))
            sb.AppendLine($"Driver {d.FullName}: safety {d.SafetyScore}, {d.TripCount} trips.");
        foreach (var inc in openIncidents)
            sb.AppendLine($"Open incident: {inc.Title} ({inc.Type}) on {inc.VehicleId}.");
        return sb.ToString();
    }

    private async Task<string> AskLlmAsync(string question, string context, CancellationToken cancellationToken)
    {
        var request = new
        {
            model = _llmOptions.Model,
            messages = new[]
            {
                new { role = "system", content = "You are OmniOps Fleet Copilot. Answer using ONLY the provided fleet context. Be concise and operational." },
                new { role = "user", content = $"Context:\n{context}\n\nQuestion: {question}" }
            },
            max_tokens = 500
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_llmOptions.BaseUrl.TrimEnd('/')}/chat/completions")
        {
            Content = JsonContent.Create(request)
        };
        httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _llmOptions.ApiKey);

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return json.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()
            ?? "No response generated.";
    }

    private static string BuildTemplatedAnswer(string question, string context)
    {
        var q = question.ToLowerInvariant();
        if (q.Contains("delay") || q.Contains("late"))
            return $"[Copilot — offline mode]\nBased on fleet context:\n{context}\nCheck active trips and open incidents for delay root causes.";
        if (q.Contains("maintenance"))
            return $"[Copilot — offline mode]\n{context}\nReview vehicle health scores and maintenance records for scheduling.";
        return $"[Copilot — offline mode]\n{context}\nRephrase your question or enable LLM for richer analysis.";
    }
}
