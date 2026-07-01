using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using MediatR;
using Microsoft.Extensions.Options;
using OmniOps.Api.Authentication;
using OmniOps.Application.Dtos;
using OmniOps.Application.Queries;
using OmniOps.Core.Entities;
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

        var telemetryEndpoint = app.MapGet("/api/telemetry/{vehicleId}", GetLatestTelemetry)
            .WithName("GetLatestVehicleTelemetry")
            .WithOpenApi();

        if (requireAuth)
        {
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
        IKafkaMessageProducer kafkaProducer,
        ILogger<Program> logger,
        IOptions<KafkaOptions> kafkaOptions)
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

        var targetTopic = kafkaOptions.Value.Topic;
        var random = new Random();
        var successfullySent = 0;

        for (var i = 0; i < packets; i++)
        {
            var mockTelemetry = new VehicleTelemetry
            {
                Id = Guid.NewGuid(),
                VehicleId = vehicleId,
                Latitude = 41.9981 + (random.NextDouble() - 0.5) * 0.05,
                Longitude = 21.4254 + (random.NextDouble() - 0.5) * 0.05,
                Speed = random.Next(50, 120),
                FuelLevel = Math.Round(80.0 - (i * 0.5), 2),
                EngineTemperature = random.Next(85, 105),
                Timestamp = DateTime.UtcNow
            };

            var jsonPayload = JsonSerializer.Serialize(mockTelemetry);
            IReadOnlyDictionary<string, string>? headers = null;

            var currentActivity = Activity.Current;
            if (currentActivity?.Id is not null)
            {
                headers = new Dictionary<string, string>
                {
                    ["traceparent"] = currentActivity.Id
                };
            }

            try
            {
                await kafkaProducer.ProduceAsync(targetTopic, jsonPayload, headers);
                successfullySent++;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Simulator failed to produce Kafka message for vehicle {VehicleId}", vehicleId);
            }

            await Task.Delay(50);
        }

        return Results.Ok(new SimulateResponse
        {
            Message = $"Successfully queued {successfullySent} mock telemetry packets into Kafka stream.",
            VehicleId = vehicleId,
            PacketsSent = successfullySent
        });
    }

    private static bool IsValidVehicleId(string vehicleId) =>
        !string.IsNullOrWhiteSpace(vehicleId) && Regex.IsMatch(vehicleId, "^[a-zA-Z0-9_-]+$");

    private sealed class SimulateResponse
    {
        public string Message { get; init; } = string.Empty;
        public string VehicleId { get; init; } = string.Empty;
        public int PacketsSent { get; init; }
    }
}
