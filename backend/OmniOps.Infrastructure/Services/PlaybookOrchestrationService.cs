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

        var repairContext = await SearchRepairManualIndexAsync(anomalySummary, cancellationToken);

        var solutionScript = GenerateSolutionScript(vehicleId, anomalySummary, repairContext);

        var ticketId = CreateServiceTicket(vehicleId, anomalySummary);

        var instructions = $"""
            AUTONOMOUS PLAYBOOK ACTIVATED
            Ticket: {ticketId}
            Anomaly: {anomalySummary}
            
            Recommended Actions:
            {solutionScript}
            """;

        await _broadcastService.BroadcastPlaybookInstructionsAsync(
            vehicleId, instructions, cancellationToken);

        _logger.LogInformation(
            "Playbook orchestration completed for vehicle {VehicleId}, TicketId={TicketId}",
            vehicleId, ticketId);
    }

    private Task<string> SearchRepairManualIndexAsync(
        string query,
        CancellationToken cancellationToken)
    {
        var simulatedResults = query.Contains("fuel", StringComparison.OrdinalIgnoreCase)
            ? "Manual Ref 7.3.2: Check fuel pump pressure and inspect for line leaks."
            : "Manual Ref 4.1.8: Inspect cooling system, verify coolant levels, check thermostat.";

        return Task.FromResult(simulatedResults);
    }

    private static string GenerateSolutionScript(
        string vehicleId,
        string anomalySummary,
        string repairContext)
    {
        return $"""
            1. Safely pull over vehicle {vehicleId} if in motion.
            2. Run diagnostic scan for engine thermal and fuel system codes.
            3. {repairContext}
            4. If temperature exceeds 110°C, disable engine and dispatch roadside assistance.
            5. Log all findings to service ticket for fleet maintenance review.
            """;
    }

    private static string CreateServiceTicket(string vehicleId, string anomalySummary)
    {
        return $"TKT-{vehicleId}-{DateTime.UtcNow:yyyyMMddHHmmss}";
    }
}
