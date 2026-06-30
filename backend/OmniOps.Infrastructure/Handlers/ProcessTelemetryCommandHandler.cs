using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using MediatR;
using OmniOps.Core.DTOs;
using OmniOps.Core.Telemetry;
using OmniOps.Core.Interfaces;
using OmniOps.Infrastructure.Data;
using OmniOps.Infrastructure.Hubs;
using StackExchange.Redis;

namespace OmniOps.Infrastructure.Handlers;

public class ProcessTelemetryCommandHandler : IRequestHandler<ProcessTelemetryCommand, bool>
{
    private readonly AppDbContext _context;
    private readonly ITelemetryCacheService _cacheService;
    private readonly IHubContext<TelemetryHub> _hubContext;
    private readonly ILogger<ProcessTelemetryCommandHandler> _logger;
    private readonly IConnectionMultiplexer _redisConnection;
    private readonly IAnomalyDetectionService _anomalyService;

    public ProcessTelemetryCommandHandler(
        AppDbContext context, 
        ITelemetryCacheService cacheService, 
        IHubContext<TelemetryHub> hubContext,
        ILogger<ProcessTelemetryCommandHandler> logger,
        IConnectionMultiplexer redisConnection,
        IAnomalyDetectionService anomalyService)
    {
        _context = context;
        _cacheService = cacheService;
        _hubContext = hubContext; 
        _logger = logger;
        _redisConnection = redisConnection;
        _anomalyService = anomalyService;
    }

    public async Task<bool> Handle(ProcessTelemetryCommand request, CancellationToken cancellationToken)
    {
        if (request.Telemetry.Id == Guid.Empty)
        {
            request.Telemetry.Id = Guid.NewGuid();
        }

        // 1. Redis Deduplication (SET NX)
        var db = _redisConnection.GetDatabase();
        var dedupKey = $"telemetry:dedup:{request.Telemetry.Id}";
        
        try
        {
            var isNew = await db.StringSetAsync(dedupKey, "1", TimeSpan.FromHours(24), When.NotExists);
            if (!isNew)
            {
                _logger.LogWarning("Duplicate telemetry packet detected. Packet ID: {PacketId}, Vehicle ID: {VehicleId}. Skipping processing.", 
                    request.Telemetry.Id, request.Telemetry.VehicleId);
                return true; // Gracefully acknowledge duplicate (idempotency)
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis deduplication check failed. Proceeding with database processing fallback.");
        }

        _logger.LogInformation("Processing telemetry command for vehicle: {VehicleId}", request.Telemetry.VehicleId);

        // 2. Add Domain Event for Transactional Outbox
        request.Telemetry.AddDomainEvent(new TelemetryReceivedEvent(request.Telemetry));

        try
        {
            // 3. Database persistence first (definitive source of truth, Outbox event saved in same transaction)
            await _context.Telemetries.AddAsync(request.Telemetry, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Successfully persisted telemetry for vehicle {VehicleId} to PostgreSQL DB.", request.Telemetry.VehicleId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist telemetry for vehicle {VehicleId} to PostgreSQL DB. Transaction aborted.", request.Telemetry.VehicleId);
            throw; // Re-throw to prevent writing to cache and SignalR
        }

        // 4. Commit to Redis Cache
        try
        {
            await _cacheService.SetLatestTelemetryAsync(request.Telemetry);
            _logger.LogInformation("Successfully updated Redis cache for vehicle {VehicleId}.", request.Telemetry.VehicleId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update Redis cache for vehicle {VehicleId}. Continuing to broadcast...", request.Telemetry.VehicleId);
        }

        // 5. Run Real-time Anomaly Detection sliding window evaluator
        try
        {
            var isAnomaly = await _anomalyService.AnalyzeTelemetryAsync(request.Telemetry, cancellationToken);
            if (isAnomaly)
            {
                _logger.LogWarning("🚨 Anomaly event raised for vehicle {VehicleId}. Triggering Autonomous AI Playbook Integration workflow...", request.Telemetry.VehicleId);
                // Here, in a distributed production deployment, we publish an anomaly event (or outbox notification)
                // that is consumed by our Semantic Kernel / LangGraph workflow system.
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute anomaly detection routine for vehicle {VehicleId}.", request.Telemetry.VehicleId);
        }

        // 6. Broadcast to SignalR TelemetryHub (using DTO to decouple wire format)
        try
        {
            var dto = TelemetryDto.FromEntity(request.Telemetry);
            await _hubContext.Clients.All
                .SendAsync("ReceiveTelemetryUpdate", dto, cancellationToken);
            _logger.LogInformation("Successfully broadcasted telemetry update for vehicle {VehicleId} to SignalR clients.", request.Telemetry.VehicleId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast telemetry update for vehicle {VehicleId} to SignalR hub.", request.Telemetry.VehicleId);
        }

        return true;
    }
}