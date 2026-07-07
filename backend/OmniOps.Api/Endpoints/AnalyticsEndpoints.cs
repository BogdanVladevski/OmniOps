using MediatR;
using Microsoft.Extensions.Options;
using OmniOps.Api.Authentication;
using OmniOps.Application.Queries;
using OmniOps.Infrastructure.Configuration;

namespace OmniOps.Api.Endpoints;

public static class AnalyticsEndpoints
{
    public static void MapAnalyticsEndpoints(this WebApplication app)
    {
        var requireAuth = app.Services.GetRequiredService<IOptions<JwtOptions>>().Value.RequireAuthentication;

        MapGet(app, "/api/analytics/fleet/{fleetId:guid}",
            async (Guid fleetId, DateTime fromUtc, DateTime toUtc, IMediator m) =>
                Results.Ok(await m.Send(new GetFleetAnalyticsQuery(fleetId, fromUtc, toUtc))), requireAuth);

        MapGet(app, "/api/analytics/drivers/{fleetId:guid}",
            async (Guid fleetId, DateTime fromUtc, DateTime toUtc, IMediator m) =>
                Results.Ok(await m.Send(new GetDriverAnalyticsQuery(fleetId, fromUtc, toUtc))), requireAuth);

        MapGet(app, "/api/analytics/operational/{fleetId:guid}",
            async (Guid fleetId, DateTime fromUtc, DateTime toUtc, IMediator m) =>
                Results.Ok(await m.Send(new GetOperationalAnalyticsQuery(fleetId, fromUtc, toUtc))), requireAuth);
    }

    private static void MapGet(WebApplication app, string pattern, Delegate handler, bool requireAuth)
    {
        var ep = app.MapGet(pattern, handler).WithOpenApi();
        if (requireAuth) ep.RequireAuthorization(AuthorizationPolicies.VehicleRead);
    }
}
