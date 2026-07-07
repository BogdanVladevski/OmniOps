using MediatR;
using Microsoft.Extensions.Options;
using OmniOps.Api.Authentication;
using OmniOps.Application.Commands;
using OmniOps.Application.Queries;
using OmniOps.Core.Enums;
using OmniOps.Infrastructure.Configuration;

namespace OmniOps.Api.Endpoints;

public static class IncidentEndpoints
{
    public static void MapIncidentEndpoints(this WebApplication app)
    {
        var requireAuth = app.Services.GetRequiredService<IOptions<JwtOptions>>().Value.RequireAuthentication;

        Map(app, "GET", "/api/incidents", async (Guid fleetId, string? status, IMediator m) =>
        {
            IncidentStatus? parsed = status is not null && Enum.TryParse<IncidentStatus>(status, true, out var s) ? s : null;
            return Results.Ok(await m.Send(new GetIncidentsQuery(fleetId, parsed)));
        }, requireAuth);

        Map(app, "POST", "/api/incidents/{incidentId:guid}/resolve",
            async (Guid incidentId, ResolveBody body, IMediator m) =>
                Results.Ok(await m.Send(new ResolveIncidentCommand(incidentId, body.Notes))), requireAuth);

        Map(app, "POST", "/api/incidents/{incidentId:guid}/notes",
            async (Guid incidentId, NoteBody body, IMediator m) =>
                Results.Ok(await m.Send(new AddIncidentNoteCommand(incidentId, body.Text, body.Author))), requireAuth);

        Map(app, "POST", "/api/incidents/{incidentId:guid}/assign",
            async (Guid incidentId, AssignBody body, IMediator m) =>
                Results.Ok(await m.Send(new AssignIncidentCommand(incidentId, body.AssignedTo))), requireAuth, admin: true);
    }

    private static void Map(WebApplication app, string method, string pattern, Delegate handler, bool requireAuth, bool admin = false)
    {
        var endpoint = method == "GET" ? app.MapGet(pattern, handler) : app.MapPost(pattern, handler);
        endpoint.WithOpenApi();
        if (requireAuth)
        {
            endpoint.RequireAuthorization(admin ? AuthorizationPolicies.FleetAdmin : AuthorizationPolicies.VehicleRead);
        }
    }

    private record ResolveBody(string? Notes);
    private record NoteBody(string Text, string? Author);
    private record AssignBody(string AssignedTo);
}
