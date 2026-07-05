using Microsoft.Extensions.Logging;
using OmniOps.Core.Interfaces;

namespace OmniOps.Infrastructure.Services;

public class PlaybookOrchestrationService : IPlaybookOrchestrationService
{
    private readonly ITelemetryBroadcastService _broadcastService;
    private readonly ILogger<PlaybookOrchestrationService> _logger;

    public PlaybookOrchestrationService(
        ITelemetryBroadcastService broadcastService,
        ILogger<PlaybookOrchestrationService> logger)
    {
        _broadcastService = broadcastService;
        _logger = logger;
    }

    public async Task OrchestrateIncidentResponseAsync(
        string vehicleId,
        string anomalySummary,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "Playbook orchestration triggered for vehicle {VehicleId}: {Summary}",
            vehicleId, anomalySummary);

        var playbookRef = await RetrievePlaybookReferenceAsync(anomalySummary, cancellationToken);

        var responseSteps = BuildResponseSteps(vehicleId, anomalySummary, playbookRef);

        var ticketId = CreateIncidentTicket(vehicleId, anomalySummary);

        var instructions = $"""
            COLD-CHAIN INCIDENT RESPONSE ACTIVATED
            Ticket: {ticketId}
            Incident: {anomalySummary}
            
            Response Protocol:
            {responseSteps}
            """;

        await _broadcastService.BroadcastPlaybookInstructionsAsync(
            vehicleId, instructions, cancellationToken);

        _logger.LogInformation(
            "Playbook orchestration completed for vehicle {VehicleId}, TicketId={TicketId}",
            vehicleId, ticketId);
    }

    private Task<string> RetrievePlaybookReferenceAsync(
        string anomalySummary,
        CancellationToken cancellationToken)
    {
        // Phase 4 will replace this with a real RAG retrieval against docs/playbooks/.
        var playbookRef = anomalySummary.Contains("excursion", StringComparison.OrdinalIgnoreCase)
                          || anomalySummary.Contains("safe range", StringComparison.OrdinalIgnoreCase)
            ? "SOP-CCM-7.3: Immediate temperature excursion response — isolate shipment, notify QA, initiate quarantine assessment."
            : "SOP-CCM-4.1: Cargo condition deviation — halt delivery, document chain-of-custody, escalate to pharmacovigilance.";

        return Task.FromResult(playbookRef);
    }

    private static string BuildResponseSteps(
        string vehicleId,
        string anomalySummary,
        string playbookRef)
    {
        return $"""
            1. Halt vehicle {vehicleId} safely and secure the shipment bay.
            2. Record current cargo temperature and GPS position.
            3. {playbookRef}
            4. Contact the receiving facility to arrange emergency cold-storage upon arrival.
            5. Log all readings, timestamps, and chain-of-custody notes to the incident ticket.
            """;
    }

    private static string CreateIncidentTicket(string vehicleId, string anomalySummary)
    {
        return $"CCM-{vehicleId}-{DateTime.UtcNow:yyyyMMddHHmmss}";
    }
}
