using Mediator;
using OmniOps.Api.Infrastructure;
using OmniOps.Application.Common.Security;
using OmniOps.Application.Features.Audit;
using OmniOps.Application.Features.Billing;
using OmniOps.Application.Features.Demo;
using OmniOps.Application.Features.Jobs;

namespace OmniOps.Api.Endpoints;

internal static class AuditEndpoints
{
    public static void MapAuditEndpoints(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/v1/audit", async (int? limit, IMediator m, CancellationToken ct) => TypedResults.Ok(await m.Send(new GetAuditLogQuery(limit ?? 100), ct)))
            .WithTags("Audit").RequireAuthorization(Policies.Owner);
}

internal static class DemoEndpoints
{
    public static void MapDemoEndpoints(this IEndpointRouteBuilder app)
    {
        var demo = app.MapGroup("/api/v1/demo").WithTags("Demo").RequireAuthorization(Policies.Office);
        demo.MapGet("/status", async (IMediator m, CancellationToken ct) => TypedResults.Ok(await m.Send(new GetDemoStatusQuery(), ct)));
        demo.MapPost("/day", async (IMediator m, CancellationToken ct) => TypedResults.Ok(await m.Send(new LoadDemoDayCommand(), ct)));
    }
}

/// <summary>What customers open from a link: no sign-in, the unguessable token is the credential.</summary>
internal static class PublicEndpoints
{
    public static void MapPublicEndpoints(this IEndpointRouteBuilder app)
    {
        var pub = app.MapGroup("").AllowAnonymous().RequireRateLimiting(RateLimits.Public).WithTags("Public");

        pub.MapGet("/api/public/track/{token}", async (string token, IMediator m, CancellationToken ct) =>
            await m.Send(new GetPublicTrackingQuery(token), ct) is { } tracking ? Results.Ok(tracking) : Results.NotFound());
        pub.MapGet("/api/public/invoices/{token}", async (string token, IMediator m, CancellationToken ct) =>
            await m.Send(new GetPublicInvoiceQuery(token), ct) is { } invoice ? Results.Ok(invoice) : Results.NotFound());
        pub.MapGet("/api/public/invoices/{token}/documents/{documentId:guid}", async (string token, Guid documentId, IMediator m, CancellationToken ct) =>
            await m.Send(new GetPublicInvoiceDocumentQuery(token, documentId), ct) is { } file
                ? Results.Stream(file.Content, file.ContentType)
                : Results.NotFound());

        // The pages themselves; they fetch the data above.
        pub.MapGet("/t/{token}", (IWebHostEnvironment env) => Page(env, "track.html")).ExcludeFromDescription();
        pub.MapGet("/i/{token}", (IWebHostEnvironment env) => Page(env, "invoice.html")).ExcludeFromDescription();
    }

    private static IResult Page(IWebHostEnvironment env, string file) =>
        Results.File(Path.Combine(env.WebRootPath, file), "text/html; charset=utf-8");
}
