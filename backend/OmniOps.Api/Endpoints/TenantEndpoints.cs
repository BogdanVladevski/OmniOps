using MediatR;
using Microsoft.Extensions.Options;
using OmniOps.Api.Authentication;
using OmniOps.Application.Commands;
using OmniOps.Application.Queries;
using OmniOps.Infrastructure.Configuration;

namespace OmniOps.Api.Endpoints;

public static class TenantEndpoints
{
    public static void MapTenantEndpoints(this WebApplication app)
    {
        var requireAuth = app.Services.GetRequiredService<IOptions<JwtOptions>>().Value.RequireAuthentication;
        var v1 = app.MapGroup("/api/v1/tenant").WithTags("Tenant");

        MapGet(v1, "/organizations", async (IMediator m) => Results.Ok(await m.Send(new GetOrganizationsQuery())), requireAuth, admin: true);
        MapGet(v1, "/organizations/{organizationId:guid}/workspaces",
            async (Guid organizationId, IMediator m) => Results.Ok(await m.Send(new GetWorkspacesQuery(organizationId))), requireAuth);
        MapGet(v1, "/organizations/{organizationId:guid}/teams",
            async (Guid organizationId, IMediator m) => Results.Ok(await m.Send(new GetTeamsQuery(organizationId))), requireAuth);
        MapGet(v1, "/organizations/{organizationId:guid}/settings",
            async (Guid organizationId, IMediator m) => Results.Ok(await m.Send(new GetTenantSettingsQuery(organizationId))), requireAuth);
        MapPost(v1, "/organizations", async (CreateOrgBody body, IMediator m) =>
        {
            var created = await m.Send(new CreateOrganizationCommand(body.Name, body.Slug));
            return Results.Created($"/api/v1/tenant/organizations/{created.Id}", created);
        }, requireAuth, admin: true);
        MapPost(v1, "/workspaces", async (CreateWorkspaceBody body, IMediator m) =>
            Results.Created("/api/v1/tenant/workspaces", await m.Send(new CreateWorkspaceCommand(body.OrganizationId, body.Name, body.DefaultFleetId))), requireAuth);
        MapPost(v1, "/teams", async (CreateTeamBody body, IMediator m) =>
            Results.Created("/api/v1/tenant/teams", await m.Send(new CreateTeamCommand(body.OrganizationId, body.Name))), requireAuth);
        MapPost(v1, "/invitations", async (InviteBody body, IMediator m) =>
            Results.Ok(await m.Send(new InviteMemberCommand(body.OrganizationId, body.Email, body.Role))), requireAuth);
        MapPut(v1, "/organizations/{organizationId:guid}/settings", async (Guid organizationId, UpdateSettingsBody body, IMediator m) =>
            Results.Ok(await m.Send(new UpdateTenantSettingsCommand(
                organizationId, body.TimeZone, body.Locale, body.EmailNotificationsEnabled, body.PushNotificationsEnabled))), requireAuth);
    }

    private static void MapGet(RouteGroupBuilder group, string pattern, Delegate handler, bool requireAuth, bool admin = false)
    {
        var ep = group.MapGet(pattern, handler).WithOpenApi();
        if (requireAuth) ep.RequireAuthorization(admin ? AuthorizationPolicies.PlatformAdmin : AuthorizationPolicies.VehicleRead);
    }

    private static void MapPost(RouteGroupBuilder group, string pattern, Delegate handler, bool requireAuth, bool admin = false)
    {
        var ep = group.MapPost(pattern, handler).WithOpenApi();
        if (requireAuth) ep.RequireAuthorization(admin ? AuthorizationPolicies.PlatformAdmin : AuthorizationPolicies.FleetAdmin);
    }

    private static void MapPut(RouteGroupBuilder group, string pattern, Delegate handler, bool requireAuth)
    {
        var ep = group.MapPut(pattern, handler).WithOpenApi();
        if (requireAuth) ep.RequireAuthorization(AuthorizationPolicies.FleetAdmin);
    }

    private sealed record CreateOrgBody(string Name, string Slug);
    private sealed record CreateWorkspaceBody(Guid OrganizationId, string Name, Guid? DefaultFleetId);
    private sealed record CreateTeamBody(Guid OrganizationId, string Name);
    private sealed record InviteBody(Guid OrganizationId, string Email, string Role);
    private sealed record UpdateSettingsBody(string TimeZone, string Locale, bool EmailNotificationsEnabled, bool PushNotificationsEnabled);
}
