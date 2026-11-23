using Mediator;
using OmniOps.Api.Infrastructure;
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
        jobs.MapGet("/{jobId:guid}/proof.pdf", async (Guid jobId, HttpContext context, IMediator m, CancellationToken ct) =>
                PdfResults.Inline(context, await m.Send(new GetJobProofPackQuery(jobId), ct)))
            .Produces(StatusCodes.Status200OK, contentType: "application/pdf")
            .WithSummary("The delivery's proof pack: timeline, waiting time, temperature log, photos and signature.");
        jobs.MapGet("/{jobId:guid}/documents/{documentId:guid}", async (Guid jobId, Guid documentId, IMediator m, CancellationToken ct) =>
        {
            var file = await m.Send(new GetJobDocumentQuery(jobId, documentId), ct);
            return TypedResults.Stream(file.Content, file.ContentType);
        });
    }
}
