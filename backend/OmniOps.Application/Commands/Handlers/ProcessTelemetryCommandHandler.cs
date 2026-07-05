using MediatR;
using Microsoft.Extensions.Logging;
using OmniOps.Application.Commands;
using OmniOps.Core.Entities;
using OmniOps.Core.Events;
using OmniOps.Core.Exceptions;
using OmniOps.Core.Interfaces;

namespace OmniOps.Application.Commands.Handlers;

public class ProcessTelemetryCommandHandler : IRequestHandler<ProcessTelemetryCommand, bool>
{
    private readonly ITelemetryRepository _repository;
    private readonly ITelemetryCacheService _cacheService;
    private readonly ITelemetryBroadcastService _broadcastService;
    private readonly IDeduplicationService _deduplicationService;
    private readonly IAnomalyDetectionService _anomalyService;
    private readonly IPlaybookOrchestrationService _playbookOrchestration;
    private readonly IShipmentRepository _shipmentRepository;
    private readonly ITelemetryMetrics _metrics;
    private readonly ILogger<ProcessTelemetryCommandHandler> _logger;

    public ProcessTelemetryCommandHandler(
        ITelemetryRepository repository,
        ITelemetryCacheService cacheService,
        ITelemetryBroadcastService broadcastService,
        IDeduplicationService deduplicationService,
        IAnomalyDetectionService anomalyService,
        IPlaybookOrchestrationService playbookOrchestration,
        IShipmentRepository shipmentRepository,
        ITelemetryMetrics metrics,
        ILogger<ProcessTelemetryCommandHandler> logger)
    {
        _repository = repository;
        _cacheService = cacheService;
        _broadcastService = broadcastService;
        _deduplicationService = deduplicationService;
        _anomalyService = anomalyService;
        _playbookOrchestration = playbookOrchestration;
        _shipmentRepository = shipmentRepository;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task<bool> Handle(ProcessTelemetryCommand request, CancellationToken cancellationToken)
    {
        if (request.Telemetry.Id == Guid.Empty)
        {
            request.Telemetry.Id = Guid.NewGuid();
        }

        var acquiredLock = await _deduplicationService.TryAcquireProcessingLockAsync(
            request.Telemetry.Id, cancellationToken);

        if (!acquiredLock)
        {
            _logger.LogWarning(
                "Duplicate telemetry packet detected. PacketId={PacketId}, VehicleId={VehicleId}",
                request.Telemetry.Id, request.Telemetry.VehicleId);
            _metrics.RecordDuplicateSkipped();
            return true;
        }

        _logger.LogInformation(
            "Processing telemetry for vehicle {VehicleId}",
            request.Telemetry.VehicleId);

        request.Telemetry.AddDomainEvent(new TelemetryReceivedEvent(request.Telemetry));

        try
        {
            await _repository.AddAsync(request.Telemetry, cancellationToken);
            await _repository.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Persisted telemetry for vehicle {VehicleId} to PostgreSQL",
                request.Telemetry.VehicleId);
        }
        catch (Exception ex)
        {
            await _deduplicationService.ReleaseProcessingLockAsync(
                request.Telemetry.Id, cancellationToken);

            _logger.LogError(ex,
                "Failed to persist telemetry for vehicle {VehicleId}. Transaction aborted; dedup lock released for retry",
                request.Telemetry.VehicleId);
            throw new TransientProcessingException(
                $"Failed to persist telemetry for vehicle {request.Telemetry.VehicleId}",
                ex);
        }

        try
        {
            await _cacheService.SetLatestTelemetryAsync(request.Telemetry);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to update Redis cache for vehicle {VehicleId}",
                request.Telemetry.VehicleId);
        }

        try
        {
            // Resolve the active cold-chain shipment for this vehicle. Failure here must never
            // block telemetry persistence — we fall back to anomaly detection without shipment context.
            Shipment? activeShipment = null;
            try
            {
                activeShipment = await _shipmentRepository.GetActiveShipmentForVehicleAsync(
                    request.Telemetry.VehicleId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Could not resolve active shipment for vehicle {VehicleId}; anomaly detection will proceed without shipment context",
                    request.Telemetry.VehicleId);
            }

            var analysis = await _anomalyService.AnalyzeTelemetryAsync(
                request.Telemetry, activeShipment, cancellationToken);

            if (analysis.IsAnomaly)
            {
                _logger.LogWarning(
                    "Anomaly event raised for vehicle {VehicleId}. Triggering cold-chain incident response",
                    request.Telemetry.VehicleId);

                _metrics.RecordAnomalyDetected();

                var incidentSummary = BuildIncidentSummary(
                    request.Telemetry, activeShipment, analysis.ExcursionDurationSeconds);

                await _playbookOrchestration.OrchestrateIncidentResponseAsync(
                    request.Telemetry.VehicleId,
                    incidentSummary,
                    cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Anomaly detection failed for vehicle {VehicleId}",
                request.Telemetry.VehicleId);
        }

        try
        {
            await _broadcastService.BroadcastTelemetryUpdateAsync(request.Telemetry, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to broadcast telemetry for vehicle {VehicleId}",
                request.Telemetry.VehicleId);
        }

        _metrics.RecordTelemetryProcessed();
        return true;
    }

    private static string BuildIncidentSummary(
        VehicleTelemetry telemetry,
        Shipment? shipment,
        int excursionDurationSeconds)
    {
        if (shipment is null)
        {
            return $"Cargo temperature anomaly detected for {telemetry.VehicleId}. " +
                   $"Current reading: {telemetry.EngineTemperature}°C.";
        }

        if (excursionDurationSeconds > 0)
        {
            return $"{shipment.ProductName} batch {shipment.BatchNumber} on {telemetry.VehicleId} " +
                   $"has been outside safe range ({shipment.MinSafeTempCelsius}–{shipment.MaxSafeTempCelsius}°C) " +
                   $"for {excursionDurationSeconds} seconds at {telemetry.EngineTemperature}°C " +
                   $"— estimated value at risk: ${shipment.ValueAtRiskUsd:N0}.";
        }

        return $"{shipment.ProductName} batch {shipment.BatchNumber} on {telemetry.VehicleId} " +
               $"cargo temperature excursion detected at {telemetry.EngineTemperature}°C " +
               $"(safe range {shipment.MinSafeTempCelsius}–{shipment.MaxSafeTempCelsius}°C) " +
               $"— estimated value at risk: ${shipment.ValueAtRiskUsd:N0}.";
    }
}
