using MediatR;
using Microsoft.Extensions.Options;
using OmniOps.Api.Authentication;
using OmniOps.Application.Queries;
using OmniOps.Infrastructure.Configuration;

namespace OmniOps.Api.Endpoints;

public static class PredictionEndpoints
{
    public static void MapPredictionEndpoints(this WebApplication app)
    {
        var requireAuth = app.Services.GetRequiredService<IOptions<JwtOptions>>().Value.RequireAuthentication;

        MapGet(app, "/api/predictions/vehicles/{vehicleId}/health",
            async (string vehicleId, IMediator m) => Results.Ok(await m.Send(new GetVehicleHealthQuery(vehicleId))), requireAuth);

        MapGet(app, "/api/predictions/vehicles/{vehicleId}/maintenance",
            async (string vehicleId, IMediator m) => Results.Ok(await m.Send(new GetMaintenancePredictionQuery(vehicleId))), requireAuth);

        MapGet(app, "/api/predictions/drivers/{driverId:guid}/risk",
            async (Guid driverId, IMediator m) => Results.Ok(await m.Send(new GetDriverRiskQuery(driverId))), requireAuth);
    }

    private static void MapGet(WebApplication app, string pattern, Delegate handler, bool requireAuth)
    {
        var ep = app.MapGet(pattern, handler).WithOpenApi();
        if (requireAuth) ep.RequireAuthorization(AuthorizationPolicies.VehicleRead);
    }
}
