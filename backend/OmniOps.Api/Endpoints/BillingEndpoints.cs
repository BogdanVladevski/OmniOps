using MediatR;
using Microsoft.Extensions.Options;
using OmniOps.Api.Authentication;
using OmniOps.Application.Commands;
using OmniOps.Application.Queries;
using OmniOps.Infrastructure.Configuration;

namespace OmniOps.Api.Endpoints;

/// <summary>Invoice settings, invoices and payment, plus the customer's public invoice page (/i/{token}).</summary>
public static class BillingEndpoints
{
    public static void MapBillingEndpoints(this WebApplication app)
    {
        var requireAuth = app.Services.GetRequiredService<IOptions<JwtOptions>>().Value.RequireAuthentication;

        var billing = app.MapGroup("/api/v1/billing").WithTags("Billing").WithOpenApi();
        var invoices = app.MapGroup("/api/v1/invoices").WithTags("Billing").WithOpenApi();
        if (requireAuth)
        {
            billing.RequireAuthorization(AuthorizationPolicies.VehicleRead);
            invoices.RequireAuthorization(AuthorizationPolicies.VehicleRead);
        }

        billing.MapGet("/profile", async (IMediator m) => Results.Ok(await m.Send(new GetBillingProfileQuery())));

        var saveProfile = billing.MapPut("/profile", async (SaveBillingProfileCommand body, IMediator m) => Results.Ok(await m.Send(body)));
        if (requireAuth)
        {
            saveProfile.RequireAuthorization(AuthorizationPolicies.FleetAdmin);
        }

        invoices.MapGet("", async (IMediator m) => Results.Ok(await m.Send(new GetInvoiceOverviewQuery())));
        invoices.MapGet("/{invoiceId:guid}", async (Guid invoiceId, IMediator m) => Results.Ok(await m.Send(new GetInvoiceQuery(invoiceId))));

        // Issue (one transaction, number included), then optionally send once it is committed.
        invoices.MapPost("", async (CreateInvoiceBody body, IMediator m) =>
        {
            var created = await m.Send(new CreateInvoiceCommand(
                body.JobId, body.CustomerName ?? string.Empty, body.CustomerEmail, body.CustomerAddress, body.CustomerTaxId,
                body.PaymentTermsDays, body.Lines ?? [], body.Notes));
            var send = body.Send?.Trim().ToLowerInvariant();
            if (send is not ("email" or "manual"))
            {
                return Results.Created($"/api/v1/invoices/{created.Summary.Id}", created);
            }

            var sent = await m.Send(new SendInvoiceCommand(created.Summary.Id, send == "email"));
            return Results.Created($"/api/v1/invoices/{created.Summary.Id}", sent);
        });

        invoices.MapPost("/{invoiceId:guid}/send", async (Guid invoiceId, SendBody? body, IMediator m) =>
            Results.Ok(await m.Send(new SendInvoiceCommand(invoiceId, body?.Email ?? false))));

        invoices.MapPost("/{invoiceId:guid}/paid", async (Guid invoiceId, PaidBody? body, IMediator m) =>
            Results.Ok(await m.Send(new MarkInvoicePaidCommand(invoiceId, body?.PaidAt))));

        app.MapGet("/api/public/invoices/{token}", async (string token, IMediator m) =>
                await m.Send(new GetPublicInvoiceQuery(token)) is { } invoice ? Results.Ok(invoice) : Results.NotFound())
            .WithTags("Public")
            .RequireRateLimiting(ServiceRegistration.PublicTrackingPolicy)
            .AllowAnonymous();

        app.MapGet("/api/public/invoices/{token}/documents/{documentId:guid}", async (string token, Guid documentId, IMediator m) =>
                await m.Send(new GetPublicInvoiceDocumentQuery(token, documentId)) is { } file
                    ? Results.Stream(file.Content, file.ContentType)
                    : Results.NotFound())
            .WithTags("Public")
            .RequireRateLimiting(ServiceRegistration.PublicTrackingPolicy)
            .AllowAnonymous();

        app.MapGet("/i/{token}", (IWebHostEnvironment env) =>
                Results.File(Path.Combine(env.WebRootPath, "invoice.html"), "text/html; charset=utf-8"))
            .ExcludeFromDescription()
            .RequireRateLimiting(ServiceRegistration.PublicTrackingPolicy)
            .AllowAnonymous();
    }

    private record CreateInvoiceBody(
        Guid JobId,
        string? CustomerName,
        string? CustomerEmail,
        string? CustomerAddress,
        string? CustomerTaxId,
        int? PaymentTermsDays,
        IReadOnlyList<InvoiceLineInput>? Lines,
        string? Notes,
        string? Send); // "email" = email the link, "manual" = office sends it, omitted = issue only

    private record SendBody(bool Email);

    private record PaidBody(DateTimeOffset? PaidAt);
}
