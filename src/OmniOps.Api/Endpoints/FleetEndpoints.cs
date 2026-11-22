using Mediator;
using OmniOps.Application.Common.Security;
using OmniOps.Application.Features.Fleet;

namespace OmniOps.Api.Endpoints;

internal static class FleetEndpoints
{
    public static void MapFleetEndpoints(this IEndpointRouteBuilder app)
    {
        var fleet = app.MapGroup("/api/v1").WithTags("Fleet").RequireAuthorization(Policies.Office);

        fleet.MapGet("/vehicles", async (bool? includeInactive, IMediator m, CancellationToken ct) =>
            TypedResults.Ok(await m.Send(new ListVehiclesQuery(includeInactive ?? false), ct)));
        fleet.MapPost("/vehicles", async (CreateVehicleCommand command, IMediator m, CancellationToken ct) =>
        {
            var vehicle = await m.Send(command, ct);
            return TypedResults.Created($"/api/v1/vehicles/{vehicle.Id}", vehicle);
        });

        fleet.MapGet("/drivers", async (bool? includeInactive, IMediator m, CancellationToken ct) =>
            TypedResults.Ok(await m.Send(new ListDriversQuery(includeInactive ?? false), ct)));
        fleet.MapPost("/drivers", async (CreateDriverCommand command, IMediator m, CancellationToken ct) =>
        {
            var driver = await m.Send(command, ct);
            return TypedResults.Created($"/api/v1/drivers/{driver.Id}", driver);
        });
        fleet.MapPut("/drivers/{driverId:guid}", async (Guid driverId, DriverBody body, IMediator m, CancellationToken ct) =>
            TypedResults.Ok(await m.Send(new UpdateDriverCommand(driverId, body.FullName ?? "", body.Phone), ct)));
    }

    internal sealed record DriverBody(string? FullName, string? Phone);
}
