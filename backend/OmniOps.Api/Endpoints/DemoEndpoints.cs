using MediatR;
using OmniOps.Application.Commands;
using OmniOps.Application.Queries;

namespace OmniOps.Api.Endpoints;

public static class DemoEndpoints
{
    public static void MapDemoEndpoints(this WebApplication app)
    {
        var v1 = app.MapGroup("/api/v1/demo").WithTags("Demo");

        v1.MapGet("/status", async (IMediator mediator) =>
            Results.Ok(await mediator.Send(new GetDemoStatusQuery()))).WithOpenApi();

        v1.MapPost("/bootstrap", async (BootstrapBody? body, IMediator mediator) =>
            Results.Ok(await mediator.Send(new BootstrapDemoCommand(body?.PacketsPerVehicle ?? 8)))).WithOpenApi();
    }

    private sealed record BootstrapBody(int? PacketsPerVehicle);
}
