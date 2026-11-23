using Mediator;
using OmniOps.Application.Common.Security;
using OmniOps.Application.Features.Customers;

namespace OmniOps.Api.Endpoints;

internal static class CustomerEndpoints
{
    public static void MapCustomerEndpoints(this IEndpointRouteBuilder app)
    {
        var customers = app.MapGroup("/api/v1/customers").WithTags("Customers").RequireAuthorization(Policies.Office);

        customers.MapGet("", async (IMediator m, CancellationToken ct) => TypedResults.Ok(await m.Send(new ListCustomersQuery(), ct)));
        customers.MapGet("/{customerId:guid}", async (Guid customerId, IMediator m, CancellationToken ct) =>
            TypedResults.Ok(await m.Send(new GetCustomerQuery(customerId), ct)));
        customers.MapPut("/{customerId:guid}", async (Guid customerId, CustomerBody body, IMediator m, CancellationToken ct) =>
            TypedResults.Ok(await m.Send(new UpdateCustomerCommand(
                customerId, body.Name ?? "", body.Email, body.Phone, body.Address, body.TaxId, body.PaymentTermsDays, body.EmailUpdates), ct)));
        customers.MapPost("/{customerId:guid}/portal", async (Guid customerId, IMediator m, CancellationToken ct) =>
                TypedResults.Ok(await m.Send(new CreatePortalLinkCommand(customerId), ct)))
            .WithSummary("Turns the customer's portal link on, or replaces it (the old link stops working).");
        customers.MapDelete("/{customerId:guid}/portal", async (Guid customerId, IMediator m, CancellationToken ct) =>
            TypedResults.Ok(await m.Send(new DisablePortalLinkCommand(customerId), ct)));
    }

    internal sealed record CustomerBody(
        string? Name, string? Email, string? Phone, string? Address, string? TaxId, int? PaymentTermsDays, bool EmailUpdates);
}
