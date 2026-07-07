using MediatR;
using Microsoft.Extensions.Options;
using OmniOps.Api.Authentication;
using OmniOps.Application.Commands;
using OmniOps.Application.Queries;
using OmniOps.Infrastructure.Configuration;

namespace OmniOps.Api.Endpoints;

public static class MobileEndpoints
{
    public static void MapMobileEndpoints(this WebApplication app)
    {
        var requireAuth = app.Services.GetRequiredService<IOptions<JwtOptions>>().Value.RequireAuthentication;
        var v1 = app.MapGroup("/api/v1/mobile").WithTags("Mobile");

        MapPost(v1, "/push-token", async (PushBody body, IMediator m) =>
            Results.Ok(await m.Send(new RegisterPushTokenCommand(body.PushToken, body.Platform))), requireAuth);

        MapGet(v1, "/sync", async (DateTime? sinceUtc, IMediator m) =>
            Results.Ok(await m.Send(new GetSyncSnapshotQuery(sinceUtc))), requireAuth);
    }

    private static void MapGet(RouteGroupBuilder group, string pattern, Delegate handler, bool requireAuth)
    {
        var ep = group.MapGet(pattern, handler).WithOpenApi();
        if (requireAuth) ep.RequireAuthorization(AuthorizationPolicies.VehicleRead);
    }

    private static void MapPost(RouteGroupBuilder group, string pattern, Delegate handler, bool requireAuth)
    {
        var ep = group.MapPost(pattern, handler).WithOpenApi();
        if (requireAuth) ep.RequireAuthorization(AuthorizationPolicies.VehicleRead);
    }

    private sealed record PushBody(string PushToken, string Platform);
}
