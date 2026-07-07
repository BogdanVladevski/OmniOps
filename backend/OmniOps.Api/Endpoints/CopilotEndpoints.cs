using MediatR;
using Microsoft.Extensions.Options;
using OmniOps.Api.Authentication;
using OmniOps.Application.Commands;
using OmniOps.Infrastructure.Configuration;

namespace OmniOps.Api.Endpoints;

public static class CopilotEndpoints
{
    public static void MapCopilotEndpoints(this WebApplication app)
    {
        var requireAuth = app.Services.GetRequiredService<IOptions<JwtOptions>>().Value.RequireAuthentication;
        var ep = app.MapPost("/api/copilot/ask",
            async (CopilotBody body, IMediator m) =>
                Results.Ok(await m.Send(new CopilotAskCommand(body.Question, body.FleetId)))).WithOpenApi();
        if (requireAuth) ep.RequireAuthorization(AuthorizationPolicies.VehicleRead);
    }

    private record CopilotBody(string Question, Guid FleetId);
}
