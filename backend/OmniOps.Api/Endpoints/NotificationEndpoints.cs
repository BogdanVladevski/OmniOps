using MediatR;
using Microsoft.Extensions.Options;
using OmniOps.Api.Authentication;
using OmniOps.Application.Commands;
using OmniOps.Application.Queries;
using OmniOps.Infrastructure.Configuration;

namespace OmniOps.Api.Endpoints;

public static class NotificationEndpoints
{
    public static void MapNotificationEndpoints(this WebApplication app)
    {
        var requireAuth = app.Services.GetRequiredService<IOptions<JwtOptions>>().Value.RequireAuthentication;
        var v1 = app.MapGroup("/api/v1/notifications").WithTags("Notifications");

        MapGet(v1, "/", async (int? limit, IMediator m) =>
            Results.Ok(await m.Send(new GetNotificationsQuery(limit ?? 50))), requireAuth, AuthorizationPolicies.VehicleRead);

        MapGet(v1, "/preferences", async (IMediator m) =>
            Results.Ok(await m.Send(new GetNotificationPreferencesQuery())), requireAuth, AuthorizationPolicies.VehicleRead);

        MapPut(v1, "/preferences", async (PrefBody body, IMediator m) =>
            Results.Ok(await m.Send(new UpdateNotificationPreferencesCommand(
                body.AlertType, body.EmailEnabled, body.PushEnabled, body.SmsEnabled, body.InAppEnabled))), requireAuth, AuthorizationPolicies.VehicleRead);

        MapGet(v1, "/rules", async (IMediator m) =>
            Results.Ok(await m.Send(new GetAlertRulesQuery())), requireAuth, AuthorizationPolicies.FleetAdmin);

        MapPost(v1, "/rules", async (RuleBody body, IMediator m) =>
            Results.Created("/api/v1/notifications/rules", await m.Send(new CreateAlertRuleCommand(
                body.Name, body.AlertType, body.Severity, body.NotifyEmail, body.NotifyPush))), requireAuth, AuthorizationPolicies.FleetAdmin);
    }

    private static void MapGet(RouteGroupBuilder group, string pattern, Delegate handler, bool requireAuth, string policy)
    {
        var ep = group.MapGet(pattern, handler).WithOpenApi();
        if (requireAuth) ep.RequireAuthorization(policy);
    }

    private static void MapPost(RouteGroupBuilder group, string pattern, Delegate handler, bool requireAuth, string policy)
    {
        var ep = group.MapPost(pattern, handler).WithOpenApi();
        if (requireAuth) ep.RequireAuthorization(policy);
    }

    private static void MapPut(RouteGroupBuilder group, string pattern, Delegate handler, bool requireAuth, string policy)
    {
        var ep = group.MapPut(pattern, handler).WithOpenApi();
        if (requireAuth) ep.RequireAuthorization(policy);
    }

    private sealed record PrefBody(string AlertType, bool EmailEnabled, bool PushEnabled, bool SmsEnabled, bool InAppEnabled);
    private sealed record RuleBody(string Name, string AlertType, string Severity, bool NotifyEmail, bool NotifyPush);
}
