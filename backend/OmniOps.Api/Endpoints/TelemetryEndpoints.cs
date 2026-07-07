using System.Diagnostics;
using System.Text.RegularExpressions;
using MediatR;
using Microsoft.Extensions.Options;
using OmniOps.Api.Authentication;
using OmniOps.Application.Queries;
using OmniOps.Core.Interfaces;
using OmniOps.Infrastructure.Configuration;
using OmniOps.Infrastructure.Hubs;

namespace OmniOps.Api.Endpoints;

public static class TelemetryEndpoints
{
    private static readonly ActivitySource SimulatorSource = new("OmniOps.Api.Simulator");

    public static void MapTelemetryEndpoints(this WebApplication app)
    {
        var jwtOptions = app.Services.GetRequiredService<IOptions<JwtOptions>>().Value;
        var requireAuth = jwtOptions.RequireAuthentication;

        var fleetEndpoint = app.MapGet("/api/telemetry/fleet", GetFleetTelemetry)
            .WithName("GetFleetTelemetry")
            .WithOpenApi();

        var telemetryEndpoint = app.MapGet("/api/telemetry/{vehicleId}", GetLatestTelemetry)
            .WithName("GetLatestVehicleTelemetry")
            .WithOpenApi();

        if (requireAuth)
        {
            fleetEndpoint.RequireAuthorization(AuthorizationPolicies.VehicleRead);
            telemetryEndpoint.RequireAuthorization(AuthorizationPolicies.VehicleRead);
        }

        var simulateEndpoint = app.MapPost("/api/test/simulate/{vehicleId}", SimulateTelemetry)
            .WithName("SimulateVehicleTelemetry")
            .WithOpenApi()
            .RequireRateLimiting("simulate");

        if (requireAuth)
        {
            simulateEndpoint.RequireAuthorization(AuthorizationPolicies.VehicleSimulate);
        }

        var hubEndpoint = app.MapHub<TelemetryHub>("/api/stream/telemetry");
        if (requireAuth)
        {
            hubEndpoint.RequireAuthorization(AuthorizationPolicies.VehicleRead);
        }
    }

    private static async Task<IResult> GetFleetTelemetry(IMediator mediator)
    {
        var result = await mediator.Send(new GetFleetTelemetryQuery());
        return Results.Ok(result);
    }

    private static async Task<IResult> GetLatestTelemetry(string vehicleId, IMediator mediator)
    {
        if (!IsValidVehicleId(vehicleId))
        {
            return Results.BadRequest(new { Message = "Vehicle ID must be a non-empty alphanumeric string." });
        }

        var result = await mediator.Send(new GetLatestTelemetryQuery(vehicleId));

        return result is not null
            ? Results.Ok(result)
            : Results.NotFound(new { Message = $"No telemetry profile cached for vehicle: {vehicleId}" });
    }

    private static async Task<IResult> SimulateTelemetry(
        string vehicleId,
        int packets,
        ITelemetrySimulatorService simulator,
        ILogger<Program> logger)
    {
        if (!IsValidVehicleId(vehicleId))
        {
            return Results.BadRequest(new { Message = "Vehicle ID must be a non-empty alphanumeric string." });
        }

        if (packets <= 0 || packets > 100)
        {
            return Results.BadRequest(new { Message = "Packets count must be between 1 and 100." });
        }

        using var activity = SimulatorSource.StartActivity("SimulateTelemetryStream");
        activity?.SetTag("vehicle.id", vehicleId);
        activity?.SetTag("packets.count", packets);

        try
        {
            var result = await simulator.SimulateAsync(vehicleId, packets);
            return Results.Ok(new
            {
                Message = $"Successfully queued {result.PacketsSent} mock telemetry packets into Kafka stream.",
                result.VehicleId,
                PacketsSent = result.PacketsSent
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Simulate failed for {VehicleId}", vehicleId);
            return Results.BadRequest(new { Message = ex.Message });
        }
    }

    private static bool IsValidVehicleId(string vehicleId) =>
        !string.IsNullOrWhiteSpace(vehicleId) && Regex.IsMatch(vehicleId, "^[a-zA-Z0-9_-]+$");
}
