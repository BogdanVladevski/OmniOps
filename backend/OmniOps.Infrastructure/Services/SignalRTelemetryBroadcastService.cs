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
        Guid organizationId,
        VehicleTelemetry telemetry,
        CancellationToken cancellationToken = default)
    {
        var dto = TelemetryDto.FromEntity(telemetry);

        await _hubContext.Clients
            .Group(TelemetryHub.VehicleGroup(organizationId, dto.VehicleId))
            .SendAsync("ReceiveTelemetryUpdate", dto, cancellationToken);

        await _hubContext.Clients
            .Group(TelemetryHub.OrganizationGroup(organizationId))
            .SendAsync("ReceiveTelemetryUpdate", dto, cancellationToken);

        _logger.LogInformation(
            "Broadcast telemetry update for vehicle {VehicleId} to SignalR group",
            dto.VehicleId);
    }

    public async Task BroadcastAlertAsync(
        Guid organizationId,
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

        await _hubContext.Clients.Group(TelemetryHub.VehicleGroup(organizationId, vehicleId)).SendAsync("ReceiveAlert", payload, cancellationToken);
        await _hubContext.Clients.Group(TelemetryHub.OrganizationGroup(organizationId)).SendAsync("ReceiveAlert", payload, cancellationToken);
    }
}
