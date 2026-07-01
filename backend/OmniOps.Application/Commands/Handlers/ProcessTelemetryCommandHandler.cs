using MediatR;
using Microsoft.Extensions.Logging;
using OmniOps.Application.Commands;
using OmniOps.Application.Dtos;
using OmniOps.Core.Entities;
using OmniOps.Core.Events;
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
    private readonly ITelemetryMetrics _metrics;
    private readonly ILogger<ProcessTelemetryCommandHandler> _logger;

    public ProcessTelemetryCommandHandler(
        ITelemetryRepository repository,
        ITelemetryCacheService cacheService,
        ITelemetryBroadcastService broadcastService,
        IDeduplicationService deduplicationService,
        IAnomalyDetectionService anomalyService,
        IPlaybookOrchestrationService playbookOrchestration,
        ITelemetryMetrics metrics,
        ILogger<ProcessTelemetryCommandHandler> logger)
    {
        _repository = repository;
        _cacheService = cacheService;
        _broadcastService = broadcastService;
        _deduplicationService = deduplicationService;
        _anomalyService = anomalyService;
        _playbookOrchestration = playbookOrchestration;
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
            "Processing telemetry command for vehicle {VehicleId}",
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
            _logger.LogError(ex,
                "Failed to persist telemetry for vehicle {VehicleId}. Transaction aborted",
                request.Telemetry.VehicleId);
            throw;
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
            var isAnomaly = await _anomalyService.AnalyzeTelemetryAsync(request.Telemetry, cancellationToken);
            if (isAnomaly)
            {
                _logger.LogWarning(
                    "Anomaly event raised for vehicle {VehicleId}. Triggering playbook orchestration",
                    request.Telemetry.VehicleId);

                _metrics.RecordAnomalyDetected();

                await _playbookOrchestration.OrchestrateIncidentResponseAsync(
                    request.Telemetry.VehicleId,
                    $"Compound distress: fuel drop + engine thermal surge detected for {request.Telemetry.VehicleId}",
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
}
