using Mediator;
using OmniOps.Application.Common.Security;
using OmniOps.Application.Features.Jobs;

namespace OmniOps.Api.Endpoints;

/// <summary>The office's view of delivery jobs.</summary>
internal static class JobEndpoints
{
    public static void MapJobEndpoints(this IEndpointRouteBuilder app)
    {
        var jobs = app.MapGroup("/api/v1/jobs").WithTags("Jobs").RequireAuthorization(Policies.Office);

        jobs.MapGet("", async (JobScope? scope, IMediator m, CancellationToken ct) =>
            TypedResults.Ok(await m.Send(new ListJobsQuery(scope ?? JobScope.Active), ct)));
        jobs.MapGet("/options", async (IMediator m, CancellationToken ct) => TypedResults.Ok(await m.Send(new GetJobOptionsQuery(), ct)));
        jobs.MapPost("", async (CreateJobCommand command, IMediator m, CancellationToken ct) =>
        {
            var job = await m.Send(command, ct);
            return TypedResults.Created($"/api/v1/jobs/{job.Id}", job);
        });
        jobs.MapGet("/{jobId:guid}", async (Guid jobId, IMediator m, CancellationToken ct) => TypedResults.Ok(await m.Send(new GetJobQuery(jobId), ct)));
        jobs.MapGet("/{jobId:guid}/documents/{documentId:guid}", async (Guid jobId, Guid documentId, IMediator m, CancellationToken ct) =>
        {
            var file = await m.Send(new GetJobDocumentQuery(jobId, documentId), ct);
            return TypedResults.Stream(file.Content, file.ContentType);
        });
    }
}
