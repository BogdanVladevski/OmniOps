using Mediator;
using Microsoft.AspNetCore.Mvc;
using OmniOps.Application.Common.Security;
using OmniOps.Application.Features.Jobs;
using OmniOps.Domain.Jobs;

namespace OmniOps.Api.Endpoints;

/// <summary>Drivers' phones (and the office acting for them). Handlers keep drivers to their own jobs.</summary>
internal static class DriverEndpoints
{
    public static void MapDriverEndpoints(this IEndpointRouteBuilder app)
    {
        var driver = app.MapGroup("/api/v1/driver/jobs").WithTags("Driver").RequireAuthorization(Policies.JobWork);

        driver.MapGet("", async (IMediator m, CancellationToken ct) => TypedResults.Ok(await m.Send(new ListJobsQuery(JobScope.Active), ct)));
        driver.MapPost("/{jobId:guid}/start", async (Guid jobId, StartBody? body, IMediator m, CancellationToken ct) =>
            TypedResults.Ok(await m.Send(new StartJobCommand(jobId, body?.StartedAt), ct)));
        driver.MapPost("/{jobId:guid}/arrived", async (Guid jobId, ArrivedBody body, IMediator m, CancellationToken ct) =>
            TypedResults.Ok(await m.Send(new MarkArrivedCommand(jobId, body.Stop ?? "", body.ArrivedAt, body.Latitude, body.Longitude), ct)));
        driver.MapPost("/{jobId:guid}/locations", async (Guid jobId, LocationsBody body, IMediator m, CancellationToken ct) =>
            TypedResults.Ok(await m.Send(new RecordLocationsCommand(jobId, body.Points ?? []), ct)));
        driver.MapPost("/{jobId:guid}/deliver", async (Guid jobId, DeliverBody body, IMediator m, CancellationToken ct) =>
            TypedResults.Ok(await m.Send(
                new DeliverJobCommand(jobId, body.RecipientName ?? "", body.Note, body.Latitude, body.Longitude, body.DeliveredAt), ct)));

        // Multipart: file + kind (+ temperatureCelsius, note, clientId, capturedAt, latitude, longitude).
        driver.MapPost("/{jobId:guid}/documents", Upload)
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(AddDocumentCommand.MaxBytes + (64 * 1024)));
    }

    private static async Task<IResult> Upload(Guid jobId, [FromForm] UploadForm form, IMediator m, CancellationToken ct)
    {
        if (!Enum.TryParse<JobDocumentKind>(form.Kind, ignoreCase: true, out var kind) || !Enum.IsDefined(kind))
        {
            return TypedResults.Problem("Unknown document kind.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (form.File is null)
        {
            return TypedResults.Problem("Attach a photo.", statusCode: StatusCodes.Status400BadRequest);
        }

        await using var content = form.File.OpenReadStream();
        var contentType = (form.File.ContentType ?? "").Split(';')[0].Trim().ToLowerInvariant() is "image/jpg" ? "image/jpeg"
            : (form.File.ContentType ?? "").Split(';')[0].Trim().ToLowerInvariant();
        return TypedResults.Ok(await m.Send(new AddDocumentCommand(
            jobId, kind, content, contentType, form.File.Length, form.Latitude, form.Longitude, form.TemperatureCelsius, form.Note,
            form.ClientId, form.CapturedAt), ct));
    }

    internal sealed class UploadForm
    {
        public IFormFile? File { get; set; }
        public string? Kind { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public double? TemperatureCelsius { get; set; }
        public string? Note { get; set; }
        public Guid? ClientId { get; set; }
        public DateTimeOffset? CapturedAt { get; set; }
    }

    internal sealed record StartBody(DateTimeOffset? StartedAt);

    internal sealed record ArrivedBody(string? Stop, DateTimeOffset? ArrivedAt, double? Latitude, double? Longitude);

    internal sealed record LocationsBody(IReadOnlyList<LocationPoint>? Points);

    internal sealed record DeliverBody(string? RecipientName, string? Note, double? Latitude, double? Longitude, DateTimeOffset? DeliveredAt);
}
