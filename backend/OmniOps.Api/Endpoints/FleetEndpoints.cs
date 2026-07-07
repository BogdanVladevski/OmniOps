using MediatR;
using Microsoft.Extensions.Options;
using OmniOps.Api.Authentication;
using OmniOps.Application.Commands;
using OmniOps.Application.Queries;
using OmniOps.Infrastructure.Configuration;

namespace OmniOps.Api.Endpoints;

public static class FleetEndpoints
{
    public static void MapFleetEndpoints(this WebApplication app)
    {
        var jwtOptions = app.Services.GetRequiredService<IOptions<JwtOptions>>().Value;
        var requireAuth = jwtOptions.RequireAuthentication;

        MapGet(app, "/api/fleets", async (IMediator m) => Results.Ok(await m.Send(new GetFleetsQuery())), requireAuth);
        MapGet(app, "/api/fleets/{fleetId:guid}/statistics",
            async (Guid fleetId, IMediator m) => Results.Ok(await m.Send(new GetFleetStatisticsQuery(fleetId))),
            requireAuth);
        MapGet(app, "/api/fleets/{fleetId:guid}/vehicles",
            async (Guid fleetId, IMediator m) => Results.Ok(await m.Send(new GetVehiclesByFleetQuery(fleetId))),
            requireAuth);
        MapPost(app, "/api/fleets",
            async (CreateFleetCommand cmd, IMediator m) => Results.Created($"/api/fleets", await m.Send(cmd)),
            requireAuth);
        MapPost(app, "/api/vehicles",
            async (CreateVehicleCommand cmd, IMediator m) => Results.Created($"/api/vehicles", await m.Send(cmd)),
            requireAuth);
        MapPost(app, "/api/drivers",
            async (CreateDriverCommand cmd, IMediator m) => Results.Created($"/api/drivers", await m.Send(cmd)),
            requireAuth);
        MapPost(app, "/api/vehicles/{vehicleId:guid}/assign-driver",
            async (Guid vehicleId, AssignDriverRequest body, IMediator m) =>
                Results.Ok(await m.Send(new AssignDriverToVehicleCommand(vehicleId, body.DriverId))),
            requireAuth);
        MapPost(app, "/api/trips",
            async (CreateTripCommand cmd, IMediator m) => Results.Created($"/api/trips", await m.Send(cmd)),
            requireAuth);
        MapPost(app, "/api/trips/{tripId:guid}/start",
            async (Guid tripId, IMediator m) => Results.Ok(await m.Send(new StartTripCommand(tripId))),
            requireAuth);
        MapPost(app, "/api/trips/{tripId:guid}/complete",
            async (Guid tripId, IMediator m) => Results.Ok(await m.Send(new CompleteTripCommand(tripId))),
            requireAuth);
        MapPost(app, "/api/depots",
            async (CreateDepotCommand cmd, IMediator m) => Results.Created($"/api/depots", await m.Send(cmd)),
            requireAuth);
        MapGet(app, "/api/geofences",
            async (Guid? fleetId, IMediator m) => Results.Ok(await m.Send(new GetGeofencesQuery(fleetId))),
            requireAuth);
        MapPost(app, "/api/geofences",
            async (CreateGeofenceCommand cmd, IMediator m) => Results.Created($"/api/geofences", await m.Send(cmd)),
            requireAuth);
        MapGet(app, "/api/fleets/{fleetId:guid}/clusters",
            async (Guid fleetId, double? radiusMeters, IMediator m) =>
                Results.Ok(await m.Send(new GetVehicleClustersQuery(fleetId, radiusMeters ?? 500))),
            requireAuth);
        MapGet(app, "/api/fleets/{fleetId:guid}/heatmap",
            async (Guid fleetId, DateTime fromUtc, DateTime toUtc, double? gridSize, IMediator m) =>
                Results.Ok(await m.Send(new GetFleetHeatmapQuery(fleetId, fromUtc, toUtc, gridSize ?? 0.01))),
            requireAuth);
        MapGet(app, "/api/telemetry/{vehicleId}/aggregations",
            async (string vehicleId, DateTime fromUtc, DateTime toUtc, int? bucketMinutes, IMediator m) =>
                Results.Ok(await m.Send(new GetTelemetryAggregationsQuery(
                    vehicleId, fromUtc, toUtc, bucketMinutes ?? 5))),
            requireAuth);
        MapGet(app, "/api/events",
            async (DateTime fromUtc, DateTime toUtc, string? eventType, IMediator m) =>
                Results.Ok(await m.Send(new GetStoredEventsQuery(fromUtc, toUtc, eventType))),
            requireAuth);
        MapPost(app, "/api/events/replay",
            async (ReplayEventsCommand cmd, IMediator m) =>
                Results.Ok(new { replayed = await m.Send(cmd) }),
            requireAuth);
    }

    private static void MapGet(
        WebApplication app, string pattern, Delegate handler, bool requireAuth)
    {
        var endpoint = app.MapGet(pattern, handler).WithOpenApi();
        if (requireAuth)
        {
            endpoint.RequireAuthorization(AuthorizationPolicies.VehicleRead);
        }
    }

    private static void MapPost(
        WebApplication app, string pattern, Delegate handler, bool requireAuth)
    {
        var endpoint = app.MapPost(pattern, handler).WithOpenApi();
        if (requireAuth)
        {
            endpoint.RequireAuthorization(AuthorizationPolicies.VehicleRead);
        }
    }

    private record AssignDriverRequest(Guid DriverId);
}
