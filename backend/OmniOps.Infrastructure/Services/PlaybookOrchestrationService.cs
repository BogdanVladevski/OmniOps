using Microsoft.Extensions.Logging;
using OmniOps.Core.Domain;
using OmniOps.Core.Interfaces;

namespace OmniOps.Infrastructure.Services;

/// <summary>
/// Retrieves the relevant cold-chain SOPs for an incident, generates a specific response narrative
/// (RAG: retrieved playbooks + LLM, with a templated fallback), and broadcasts it to clients over
/// the existing SignalR playbook-instructions channel.
/// </summary>
public class PlaybookOrchestrationService : IPlaybookOrchestrationService
{
    private readonly IPlaybookRetrievalService _retrievalService;
    private readonly IIncidentNarrativeService _narrativeService;
    private readonly ITelemetryBroadcastService _broadcastService;
    private readonly ILogger<PlaybookOrchestrationService> _logger;

    public PlaybookOrchestrationService(
        IPlaybookRetrievalService retrievalService,
        IIncidentNarrativeService narrativeService,
        ITelemetryBroadcastService broadcastService,
        ILogger<PlaybookOrchestrationService> logger)
    {
        _retrievalService = retrievalService;
        _narrativeService = narrativeService;
        _broadcastService = broadcastService;
        _logger = logger;
    }

    public async Task OrchestrateIncidentResponseAsync(
        IncidentContext incident,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "Playbook orchestration triggered for vehicle {VehicleId}: {Summary}",
            incident.VehicleId, incident.IncidentSummary);

        var playbooks = _retrievalService.Retrieve(incident);

        _logger.LogInformation(
            "Retrieved {Count} playbook(s) for vehicle {VehicleId}: {Ids}",
            playbooks.Count, incident.VehicleId, string.Join(", ", playbooks.Select(p => p.Id)));

        var narrative = await _narrativeService.GenerateNarrativeAsync(
            incident, playbooks, cancellationToken);

        var ticketId = CreateIncidentTicket(incident.VehicleId);

        var instructions = $"""
            COLD-CHAIN INCIDENT RESPONSE ACTIVATED
            Ticket: {ticketId}

            {narrative}
            """;

        await _broadcastService.BroadcastPlaybookInstructionsAsync(
            incident.VehicleId, instructions, cancellationToken);

        _logger.LogInformation(
            "Playbook orchestration completed for vehicle {VehicleId}, TicketId={TicketId}",
            incident.VehicleId, ticketId);
    }

    private static string CreateIncidentTicket(string vehicleId) =>
        $"CCM-{vehicleId}-{DateTime.UtcNow:yyyyMMddHHmmss}";
}
