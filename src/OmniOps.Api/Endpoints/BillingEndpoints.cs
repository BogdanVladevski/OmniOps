using Mediator;
using OmniOps.Application.Common.Security;
using OmniOps.Application.Features.Billing;

namespace OmniOps.Api.Endpoints;

internal static class BillingEndpoints
{
    public static void MapBillingEndpoints(this IEndpointRouteBuilder app)
    {
        var billing = app.MapGroup("/api/v1").WithTags("Billing").RequireAuthorization(Policies.Office);

        billing.MapGet("/billing/profile", async (IMediator m, CancellationToken ct) => TypedResults.Ok(await m.Send(new GetBillingProfileQuery(), ct)));
        billing.MapPut("/billing/profile", async (SaveBillingProfileCommand command, IMediator m, CancellationToken ct) =>
                TypedResults.Ok(await m.Send(command, ct)))
            .RequireAuthorization(Policies.Owner);

        billing.MapGet("/invoices", async (IMediator m, CancellationToken ct) => TypedResults.Ok(await m.Send(new GetInvoiceOverviewQuery(), ct)));
        billing.MapGet("/invoices/{invoiceId:guid}", async (Guid invoiceId, IMediator m, CancellationToken ct) =>
            TypedResults.Ok(await m.Send(new GetInvoiceQuery(invoiceId), ct)));
        billing.MapPost("/invoices", async (CreateInvoiceBody body, IMediator m, CancellationToken ct) =>
        {
            // Issued (and committed) first; sending is separate so a failed email never undoes the invoice.
            var invoice = await m.Send(new CreateInvoiceCommand(
                body.JobId, body.CustomerName ?? "", body.CustomerEmail, body.CustomerAddress, body.CustomerTaxId,
                body.PaymentTermsDays, body.Lines ?? [], body.Notes), ct);
            if (body.Send is "email" or "manual")
            {
                invoice = await m.Send(new SendInvoiceCommand(invoice.Summary.Id, body.Send == "email"), ct);
            }

            return TypedResults.Created($"/api/v1/invoices/{invoice.Summary.Id}", invoice);
        });
        billing.MapPost("/invoices/{invoiceId:guid}/send", async (Guid invoiceId, SendBody body, IMediator m, CancellationToken ct) =>
            TypedResults.Ok(await m.Send(new SendInvoiceCommand(invoiceId, body.Email), ct)));
        billing.MapPost("/invoices/{invoiceId:guid}/paid", async (Guid invoiceId, PaidBody? body, IMediator m, CancellationToken ct) =>
            TypedResults.Ok(await m.Send(new MarkInvoicePaidCommand(invoiceId, body?.PaidAt), ct)));
    }

    internal sealed record CreateInvoiceBody(
        Guid JobId, string? CustomerName, string? CustomerEmail, string? CustomerAddress, string? CustomerTaxId,
        int? PaymentTermsDays, IReadOnlyList<InvoiceLineInput>? Lines, string? Notes, string? Send);

    internal sealed record SendBody(bool Email);

    internal sealed record PaidBody(DateTimeOffset? PaidAt);
}
