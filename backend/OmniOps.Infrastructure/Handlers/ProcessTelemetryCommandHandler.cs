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

namespace OmniOps.Infrastructure.Handlers;

public class ProcessTelemetryCommandHandler : IRequestHandler<ProcessTelemetryCommand, bool>
{
    private readonly AppDbContext _context;
    private readonly ITelemetryCacheService _cacheService;
    private readonly IHubContext<TelemetryHub> _hubContext;
    private readonly ILogger<ProcessTelemetryCommandHandler> _logger;

    public ProcessTelemetryCommandHandler(
        AppDbContext context, 
        ITelemetryCacheService cacheService, 
        IHubContext<TelemetryHub> hubContext,
        ILogger<ProcessTelemetryCommandHandler> logger)
    {
        _context = context;
        _cacheService = cacheService;
        _hubContext = hubContext; 
        _logger = logger;
    }

    public async Task<bool> Handle(ProcessTelemetryCommand request, CancellationToken cancellationToken)
    {
        request.Telemetry.Id = Guid.NewGuid();

        _logger.LogInformation("Processing telemetry command for vehicle: {VehicleId}", request.Telemetry.VehicleId);

        try
        {
            // 1. Persist to Postgres database first (definitive source of truth)
            await _context.Telemetries.AddAsync(request.Telemetry, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Successfully persisted telemetry for vehicle {VehicleId} to PostgreSQL DB.", request.Telemetry.VehicleId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist telemetry for vehicle {VehicleId} to PostgreSQL DB. Transaction aborted.", request.Telemetry.VehicleId);
            throw; // Re-throw to prevent writing to cache and SignalR
        }

        // 2. Commit to Redis Cache
        try
        {
            await _cacheService.SetLatestTelemetryAsync(request.Telemetry);
            _logger.LogInformation("Successfully updated Redis cache for vehicle {VehicleId}.", request.Telemetry.VehicleId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update Redis cache for vehicle {VehicleId}. Continuing to broadcast...", request.Telemetry.VehicleId);
        }

        // 3. Broadcast to SignalR TelemetryHub (using DTO to decouple wire format)
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