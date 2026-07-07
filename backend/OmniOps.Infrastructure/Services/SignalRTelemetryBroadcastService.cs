using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using OmniOps.Application.Dtos;
using OmniOps.Core.Entities;
using OmniOps.Core.Interfaces;
using OmniOps.Infrastructure.Hubs;

namespace OmniOps.Infrastructure.Services;

public class SignalRTelemetryBroadcastService : ITelemetryBroadcastService
{
    private readonly IHubContext<TelemetryHub> _hubContext;
    private readonly ILogger<SignalRTelemetryBroadcastService> _logger;

    public SignalRTelemetryBroadcastService(
        IHubContext<TelemetryHub> hubContext,
        ILogger<SignalRTelemetryBroadcastService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task BroadcastTelemetryUpdateAsync(
        VehicleTelemetry telemetry,
        CancellationToken cancellationToken = default)
    {
        var dto = TelemetryDto.FromEntity(telemetry);

        await _hubContext.Clients
            .Group(dto.VehicleId)
            .SendAsync("ReceiveTelemetryUpdate", dto, cancellationToken);

        await _hubContext.Clients
            .Group(TelemetryHub.FleetGroupName)
            .SendAsync("ReceiveTelemetryUpdate", dto, cancellationToken);

        _logger.LogInformation(
            "Broadcast telemetry update for vehicle {VehicleId} to SignalR group",
            dto.VehicleId);
    }

    public async Task BroadcastPlaybookInstructionsAsync(
        string vehicleId,
        string instructions,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            VehicleId = vehicleId,
            Instructions = instructions,
            GeneratedAt = DateTime.UtcNow
        };

        await _hubContext.Clients
            .Group(vehicleId)
            .SendAsync("ReceivePlaybookInstructions", payload, cancellationToken);

        await _hubContext.Clients
            .Group(TelemetryHub.FleetGroupName)
            .SendAsync("ReceivePlaybookInstructions", payload, cancellationToken);

        _logger.LogInformation(
            "Broadcast playbook instructions for vehicle {VehicleId}",
            vehicleId);
    }

    public async Task BroadcastAlertAsync(
        string vehicleId,
        string alertType,
        string title,
        string message,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            VehicleId = vehicleId,
            AlertType = alertType,
            Title = title,
            Message = message,
            GeneratedAt = DateTime.UtcNow
        };

        await _hubContext.Clients.Group(vehicleId).SendAsync("ReceiveAlert", payload, cancellationToken);
        await _hubContext.Clients.Group(TelemetryHub.FleetGroupName).SendAsync("ReceiveAlert", payload, cancellationToken);
    }
}
