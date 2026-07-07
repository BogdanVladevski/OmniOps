using MediatR;
using Microsoft.Extensions.Logging;
using OmniOps.Application.Commands;
using OmniOps.Core.Domain;
using OmniOps.Core.Entities;
using OmniOps.Core.Enums;
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
    private readonly IGeofenceDetectionService _geofenceDetectionService;
    private readonly IIncidentDetectionService _incidentDetectionService;
    private readonly IIncidentRepository _incidentRepository;
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
        IGeofenceDetectionService geofenceDetectionService,
        IIncidentDetectionService incidentDetectionService,
        IIncidentRepository incidentRepository,
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
        _geofenceDetectionService = geofenceDetectionService;
        _incidentDetectionService = incidentDetectionService;
        _incidentRepository = incidentRepository;
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

        var geofenceBreaches = await _geofenceDetectionService.DetectBreachesAsync(
            request.Telemetry, cancellationToken);
        foreach (var breach in geofenceBreaches)
        {
            request.Telemetry.AddDomainEvent(breach);
        }

        var detectedIncidents = (await _incidentDetectionService.DetectAsync(request.Telemetry, cancellationToken)).ToList();
        foreach (var breach in geofenceBreaches)
        {
            detectedIncidents.Add(new Incident
            {
                Id = Guid.NewGuid(),
                OrganizationId = TenantSeed.DefaultOrganizationId,
                FleetId = new Guid("f1000000-0000-0000-0000-000000000001"),
                VehicleId = breach.VehicleId,
                Type = IncidentType.GeofenceBreach,
                Severity = IncidentSeverity.Medium,
                Status = IncidentStatus.Open,
                Title = $"{breach.BreachType} geofence — {breach.VehicleId}",
                Description = $"Geofence {breach.GeofenceId} {breach.BreachType.ToString().ToLowerInvariant()}.",
                DetectedAtUtc = request.Telemetry.Timestamp,
                Latitude = request.Telemetry.Latitude,
                Longitude = request.Telemetry.Longitude
            });
        }

        foreach (var incident in detectedIncidents)
        {
            await _incidentRepository.AddAsync(incident, cancellationToken);
        }

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
            // Shipment lookup failure must not block telemetry persistence — fall through without context.
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
                var severity = analysis.Severity ?? AnomalySeverity.Warning;

                _logger.LogWarning(
                    "{Severity} anomaly for vehicle {VehicleId} — triggering cold-chain incident response",
                    severity, request.Telemetry.VehicleId);

                _metrics.RecordAnomalyDetected();

                var incidentSummary = BuildIncidentSummary(
                    request.Telemetry, activeShipment, analysis.ExcursionDurationSeconds, severity);

                // Attach the domain event so downstream subscribers (outbox -> Kafka events,
                // RAG narrative) get severity + value-at-risk without re querying.
                request.Telemetry.AddDomainEvent(new AnomalyDetectedEvent(
                    vehicleId: request.Telemetry.VehicleId,
                    severity: severity,
                    excursionDurationSeconds: analysis.ExcursionDurationSeconds,
                    incidentSummary: incidentSummary,
                    productName: activeShipment?.ProductName,
                    batchNumber: activeShipment?.BatchNumber,
                    valueAtRiskUsd: activeShipment?.ValueAtRiskUsd));

                var incidentContext = new IncidentContext
                {
                    VehicleId = request.Telemetry.VehicleId,
                    Severity = severity,
                    ExcursionDurationSeconds = analysis.ExcursionDurationSeconds,
                    TemperatureCelsius = request.Telemetry.EngineTemperature,
                    ProductName = activeShipment?.ProductName,
                    BatchNumber = activeShipment?.BatchNumber,
                    ValueAtRiskUsd = activeShipment?.ValueAtRiskUsd,
                    MinSafeTempCelsius = activeShipment?.MinSafeTempCelsius,
                    MaxSafeTempCelsius = activeShipment?.MaxSafeTempCelsius,
                    IncidentSummary = incidentSummary
                };

                await _playbookOrchestration.OrchestrateIncidentResponseAsync(
                    incidentContext,
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
            foreach (var incident in detectedIncidents)
            {
                await _broadcastService.BroadcastAlertAsync(
                    incident.VehicleId,
                    incident.Type.ToString(),
                    incident.Title,
                    incident.Description,
                    cancellationToken);
            }
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
        int excursionDurationSeconds,
        AnomalySeverity severity)
    {
        var tag = severity == AnomalySeverity.Critical ? "[CRITICAL]" : "[WARNING]";

        if (shipment is null)
        {
            return $"{tag} Cargo temperature anomaly on {telemetry.VehicleId} — " +
                   $"reading: {telemetry.EngineTemperature}°C.";
        }

        if (excursionDurationSeconds > 0)
        {
            return $"{tag} {shipment.ProductName} batch {shipment.BatchNumber} on {telemetry.VehicleId} " +
                   $"has been outside safe range ({shipment.MinSafeTempCelsius}–{shipment.MaxSafeTempCelsius}°C) " +
                   $"for {excursionDurationSeconds}s at {telemetry.EngineTemperature}°C " +
                   $"— est. value at risk: ${shipment.ValueAtRiskUsd:N0}.";
        }

        // Warning with no recorded excursion yet = trending toward breach.
        return $"{tag} {shipment.ProductName} batch {shipment.BatchNumber} on {telemetry.VehicleId} " +
               $"cargo temperature trending toward excursion at {telemetry.EngineTemperature}°C " +
               $"(safe {shipment.MinSafeTempCelsius}–{shipment.MaxSafeTempCelsius}°C) " +
               $"— est. value at risk: ${shipment.ValueAtRiskUsd:N0}.";
    }
}

