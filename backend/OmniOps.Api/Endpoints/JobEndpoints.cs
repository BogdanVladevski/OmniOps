using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using OmniOps.Api.Authentication;
using OmniOps.Application.Commands;
using OmniOps.Application.Queries;
using OmniOps.Core.Enums;
using OmniOps.Infrastructure.Configuration;

namespace OmniOps.Api.Endpoints;

/// <summary>
/// Delivery jobs: the office's job board (<c>/api/v1/jobs</c>), the driver app (<c>/api/v1/driver</c>) and the
/// customer's public tracking link (<c>/t/{token}</c>, <c>/api/public/track/{token}</c>).
/// </summary>
public static class JobEndpoints
{
    public static void MapJobEndpoints(this WebApplication app)
    {
        var requireAuth = app.Services.GetRequiredService<IOptions<JwtOptions>>().Value.RequireAuthentication;

        var jobs = app.MapGroup("/api/v1/jobs").WithTags("Jobs");
        Protect(jobs, requireAuth);

        jobs.MapGet("", async (string? scope, IMediator m) =>
            Results.Ok(await m.Send(new GetJobsQuery(ParseScope(scope)))));

        jobs.MapGet("/assignable", async (IMediator m) => Results.Ok(await m.Send(new GetJobAssignablesQuery())));

        jobs.MapPost("", async (CreateJobBody body, IMediator m) =>
        {
            var job = await m.Send(new CreateJobCommand(
                body.VehicleId, body.DriverId, body.CustomerName ?? string.Empty, body.Reference, body.Origin,
                body.Destination ?? string.Empty, body.DestinationLatitude, body.DestinationLongitude, body.PlannedDeliveryUtc,
                body.MinTempCelsius, body.MaxTempCelsius));
            return Results.Created($"/api/v1/jobs/{job.Id}", job);
        });

        jobs.MapGet("/{jobId:guid}", async (Guid jobId, IMediator m) => Results.Ok(await m.Send(new GetJobQuery(jobId))));

        jobs.MapGet("/{jobId:guid}/documents/{documentId:guid}", async (Guid jobId, Guid documentId, IMediator m) =>
        {
            var file = await m.Send(new GetJobDocumentQuery(jobId, documentId));
            return Results.Stream(file.Content, file.ContentType);
        });

        // Drivers' phones (and the office, for testing). Handlers keep drivers to their own jobs.
        var driver = app.MapGroup("/api/v1/driver").WithTags("Driver").WithOpenApi();
        if (requireAuth)
        {
            driver.RequireAuthorization(AuthorizationPolicies.JobWork);
        }

        driver.MapGet("/jobs", async (Guid? driverId, IMediator m) =>
            Results.Ok(await m.Send(new GetJobsQuery(JobScope.Active, driverId))));

        driver.MapPost("/jobs/{jobId:guid}/start", async (Guid jobId, IMediator m) =>
            Results.Ok(await m.Send(new StartJobCommand(jobId))));

        driver.MapPost("/jobs/{jobId:guid}/locations", async (Guid jobId, LocationsBody body, IMediator m) =>
            Results.Ok(await m.Send(new RecordJobLocationsCommand(jobId, body.Points ?? []))));

        // Delivery photos (kept for older app versions) and any job document: ?kind=DeliveryPhoto|PickupDocument|TemperaturePhoto.
        driver.MapPost("/jobs/{jobId:guid}/photos", (Guid jobId, IFormFile file, double? latitude, double? longitude, IMediator m) =>
                UploadAsync(m, jobId, TripDocumentKind.DeliveryPhoto, file, latitude, longitude, null))
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(AddJobDocumentCommand.MaxBytes + 64 * 1024));

        driver.MapPost("/jobs/{jobId:guid}/documents",
                (Guid jobId, string kind, IFormFile file, double? latitude, double? longitude, double? temperatureCelsius, IMediator m) =>
                    Enum.TryParse<TripDocumentKind>(kind, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)
                        ? UploadAsync(m, jobId, parsed, file, latitude, longitude, temperatureCelsius)
                        : Task.FromResult(Results.BadRequest(new { title = "Unknown document kind." })))
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(AddJobDocumentCommand.MaxBytes + 64 * 1024));

        driver.MapPost("/jobs/{jobId:guid}/deliver", async (Guid jobId, DeliverBody body, IMediator m) =>
            Results.Ok(await m.Send(new DeliverJobCommand(
                jobId, body.RecipientName ?? string.Empty, body.Note, body.Latitude, body.Longitude))));

        // Public: the token is the credential. Unknown/expired links all look the same (404).
        app.MapGet("/api/public/track/{token}", async (string token, IMediator m) =>
            {
                var tracking = await m.Send(new GetPublicTrackingQuery(token));
                return tracking is null ? Results.NotFound() : Results.Ok(tracking);
            })
            .WithTags("Public")
            .RequireRateLimiting(ServiceRegistration.PublicTrackingPolicy)
            .AllowAnonymous();

        app.MapGet("/t/{token}", (IWebHostEnvironment env) =>
                Results.File(Path.Combine(env.WebRootPath, "track.html"), "text/html; charset=utf-8"))
            .ExcludeFromDescription()
            .RequireRateLimiting(ServiceRegistration.PublicTrackingPolicy)
            .AllowAnonymous();
    }

    private static void Protect(RouteGroupBuilder group, bool requireAuth)
    {
        group.WithOpenApi();
        if (requireAuth)
        {
            group.RequireAuthorization(AuthorizationPolicies.VehicleRead);
        }
    }

    private static async Task<IResult> UploadAsync(
        IMediator m, Guid jobId, TripDocumentKind kind, IFormFile file, double? latitude, double? longitude, double? temperatureCelsius)
    {
        await using var content = file.OpenReadStream();
        return Results.Ok(await m.Send(new AddJobDocumentCommand(
            jobId, kind, content, NormalizeContentType(file.ContentType), file.Length, latitude, longitude, temperatureCelsius)));
    }

    private static JobScope ParseScope(string? scope) =>
        Enum.TryParse<JobScope>(scope, ignoreCase: true, out var parsed) ? parsed : JobScope.Active;

    // Some phones label photos "image/jpg"; treat it as JPEG.
    private static string NormalizeContentType(string? contentType) =>
        (contentType ?? string.Empty).Split(';')[0].Trim().ToLowerInvariant() switch
        {
            "image/jpg" => "image/jpeg",
            var other => other,
        };

    private record CreateJobBody(
        Guid VehicleId,
        Guid? DriverId,
        string? CustomerName,
        string? Reference,
        string? Origin,
        string? Destination,
        double? DestinationLatitude,
        double? DestinationLongitude,
        DateTimeOffset? PlannedDeliveryUtc,
        double? MinTempCelsius,
        double? MaxTempCelsius);

    private record LocationsBody(IReadOnlyList<JobLocationPoint>? Points);

    private record DeliverBody(string? RecipientName, string? Note, double? Latitude, double? Longitude);
}
