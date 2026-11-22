using MediatR;
using Microsoft.Extensions.Options;
using OmniOps.Api.Authentication;
using OmniOps.Application.Commands;
using OmniOps.Infrastructure.Configuration;
using OmniOps.Application.Queries;

namespace OmniOps.Api.Endpoints;

public static class DemoEndpoints
{
    public static void MapDemoEndpoints(this WebApplication app)
    {
        var v1 = app.MapGroup("/api/v1/demo").WithTags("Demo");

        v1.MapGet("/status", async (IMediator mediator) =>
            Results.Ok(await mediator.Send(new GetDemoStatusQuery()))).WithOpenApi();

        // Writes demo jobs into the caller's organization, so it needs the same access as the job board.
        var bootstrap = v1.MapPost("/bootstrap", async (BootstrapBody? body, IMediator mediator) =>
            Results.Ok(await mediator.Send(new BootstrapDemoCommand(body?.PacketsPerVehicle ?? 8)))).WithOpenApi();
        if (app.Services.GetRequiredService<IOptions<JwtOptions>>().Value.RequireAuthentication)
        {
            bootstrap.RequireAuthorization(AuthorizationPolicies.VehicleRead);
        }
    }

    private sealed record BootstrapBody(int? PacketsPerVehicle);
}
