using MediatR;
using Microsoft.Extensions.Options;
using OmniOps.Api.Authentication;
using OmniOps.Application.Queries;
using OmniOps.Infrastructure.Configuration;

namespace OmniOps.Api.Endpoints;

public static class ShipmentEndpoints
{
    public static void MapShipmentEndpoints(this WebApplication app)
    {
        var jwtOptions = app.Services.GetRequiredService<IOptions<JwtOptions>>().Value;
        var requireAuth = jwtOptions.RequireAuthentication;

        var replayEndpoint = app.MapGet("/api/shipments/{shipmentId:guid}/replay", GetShipmentReplay)
            .WithName("GetShipmentReplay")
            .WithOpenApi();

        if (requireAuth)
        {
            replayEndpoint.RequireAuthorization(AuthorizationPolicies.VehicleRead);
        }
    }

    private static async Task<IResult> GetShipmentReplay(
        Guid shipmentId,
        DateTime? fromUtc,
        DateTime? toUtc,
        DateTime? anchorUtc,
        IMediator mediator)
    {
        var result = await mediator.Send(new GetShipmentReplayQuery(shipmentId, fromUtc, toUtc, anchorUtc));

        return result is not null
            ? Results.Ok(result)
            : Results.NotFound(new { Message = $"Shipment not found: {shipmentId}" });
    }
}
