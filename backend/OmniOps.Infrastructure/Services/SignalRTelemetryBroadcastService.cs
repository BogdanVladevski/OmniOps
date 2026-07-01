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

        _logger.LogInformation(
            "Broadcast telemetry update for vehicle {VehicleId} to SignalR group",
            dto.VehicleId);
    }

    public async Task BroadcastPlaybookInstructionsAsync(
        string vehicleId,
        string instructions,
        CancellationToken cancellationToken = default)
    {
        await _hubContext.Clients
            .Group(vehicleId)
            .SendAsync("ReceivePlaybookInstructions", new
            {
                VehicleId = vehicleId,
                Instructions = instructions,
                GeneratedAt = DateTime.UtcNow
            }, cancellationToken);

        _logger.LogInformation(
            "Broadcast playbook instructions for vehicle {VehicleId}",
            vehicleId);
    }
}
