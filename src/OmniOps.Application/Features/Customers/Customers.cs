using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;
using OmniOps.Application.Common.Interfaces;
using OmniOps.Application.Common.Security;
using OmniOps.Application.Features.Billing;
using OmniOps.Application.Features.Fleet;
using OmniOps.Application.Features.Jobs;
using OmniOps.Domain.Common;

namespace OmniOps.Application.Features.Customers;

// Customers appear by themselves when the office creates a job for a new name. Here the office keeps their details,
// turns on email updates, and hands out the customer's portal link.

[RequiresPolicy(Policies.Office)]
public sealed record ListCustomersQuery : IQuery<IReadOnlyList<CustomerDto>>;

public sealed class ListCustomersHandler(IAppDbContext db, IPublicLinks links, TimeProvider clock) : IQueryHandler<ListCustomersQuery, IReadOnlyList<CustomerDto>>
{
    public async ValueTask<IReadOnlyList<CustomerDto>> Handle(ListCustomersQuery query, CancellationToken cancellationToken) =>
        await CustomerDirectory.SummariesAsync(db, links, db.Customers, clock.GetUtcNow().UtcDateTime, cancellationToken);
}

[RequiresPolicy(Policies.Office)]
public sealed record GetCustomerQuery(Guid CustomerId) : IQuery<CustomerDetailDto>;

public sealed class GetCustomerHandler(IAppDbContext db, ITenantContext tenant, IPublicLinks links, TimeProvider clock) : IQueryHandler<GetCustomerQuery, CustomerDetailDto>
{
    public async ValueTask<CustomerDetailDto> Handle(GetCustomerQuery query, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var summary = (await CustomerDirectory.SummariesAsync(db, links, db.Customers.Where(c => c.Id == query.CustomerId), now, cancellationToken))
            .SingleOrDefault() ?? throw new KeyNotFoundException("Customer not found.");
        var jobs = await db.Visible(tenant).Where(j => j.CustomerId == query.CustomerId)
            .OrderByDescending(j => j.CreatedAtUtc).Take(20).ToListAsync(cancellationToken);
        var invoices = await db.Invoices.Include(i => i.Reminders).Where(i => i.CustomerId == query.CustomerId)
            .OrderByDescending(i => i.IssueDateUtc).Take(20).ToListAsync(cancellationToken);
        return new CustomerDetailDto(summary, jobs.Select(JobDto.From).ToList(), invoices.Select(i => InvoiceSummaryDto.From(i, now)).ToList());
    }
}

[RequiresPolicy(Policies.Office)]
public sealed record UpdateCustomerCommand(
    Guid CustomerId,
    string Name,
    string? Email,
    string? Phone,
    string? Address,
    string? TaxId,
    int? PaymentTermsDays,
    bool EmailUpdates) : ICommand<CustomerDto>;

public sealed class UpdateCustomerValidator : AbstractValidator<UpdateCustomerCommand>
{
    public UpdateCustomerValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Give the customer a name.").MaximumLength(200);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email))
            .WithMessage("That email address doesn't look right.").MaximumLength(200);
        RuleFor(x => x.Phone).PhoneNumber();
        RuleFor(x => x.Address).MaximumLength(500);
        RuleFor(x => x.TaxId).MaximumLength(50);
        RuleFor(x => x.PaymentTermsDays).InclusiveBetween(0, 365);
        RuleFor(x => x.Email).NotEmpty().When(x => x.EmailUpdates).WithMessage("Add an email address to send updates to.");
    }
}

public sealed class UpdateCustomerHandler(IAppDbContext db, IPublicLinks links, IAuditLog audit, TimeProvider clock)
    : ICommandHandler<UpdateCustomerCommand, CustomerDto>
{
    public async ValueTask<CustomerDto> Handle(UpdateCustomerCommand command, CancellationToken cancellationToken)
    {
        var customer = await CustomerDirectory.GetAsync(db, command.CustomerId, cancellationToken);
        var normalized = BillingText.NormalizeName(command.Name);
        if (normalized != customer.NormalizedName
            && await db.Customers.AnyAsync(c => c.NormalizedName == normalized && c.Id != customer.Id, cancellationToken))
        {
            throw new BusinessRuleException($"You already have a customer called {command.Name.Trim()}.");
        }

        var now = clock.GetUtcNow().UtcDateTime;
        customer.Name = command.Name.Trim();
        customer.NormalizedName = normalized;
        customer.Email = BillingText.Clean(command.Email);
        customer.Phone = BillingText.Clean(command.Phone);
        customer.Address = BillingText.Clean(command.Address);
        customer.TaxId = BillingText.Clean(command.TaxId);
        customer.PaymentTermsDays = command.PaymentTermsDays;
        customer.EmailUpdates = command.EmailUpdates;
        customer.UpdatedAtUtc = now;
        audit.Record("Update", "Customer", customer.Id.ToString(), customer.Name);
        await db.SaveChangesAsync(cancellationToken);
        return await SummaryAsync(db, links, customer.Id, now, cancellationToken);
    }

    internal static async Task<CustomerDto> SummaryAsync(IAppDbContext db, IPublicLinks links, Guid id, DateTime now, CancellationToken ct) =>
        (await CustomerDirectory.SummariesAsync(db, links, db.Customers.Where(c => c.Id == id), now, ct)).Single();
}

/// <summary>Turns the customer's portal link on, or replaces it: the previous link stops working at once.</summary>
[RequiresPolicy(Policies.Office)]
public sealed record CreatePortalLinkCommand(Guid CustomerId) : ICommand<CustomerDto>;

public sealed class CreatePortalLinkHandler(IAppDbContext db, IPublicLinks links, IAuditLog audit, TimeProvider clock)
    : ICommandHandler<CreatePortalLinkCommand, CustomerDto>
{
    public async ValueTask<CustomerDto> Handle(CreatePortalLinkCommand command, CancellationToken cancellationToken)
    {
        var customer = await CustomerDirectory.GetAsync(db, command.CustomerId, cancellationToken);
        var now = clock.GetUtcNow().UtcDateTime;
        var replaced = customer.PortalToken is not null;
        customer.PortalToken = PublicToken.Create();
        customer.PortalCreatedAtUtc = now;
        audit.Record(replaced ? "ReplacePortalLink" : "CreatePortalLink", "Customer", customer.Id.ToString(), customer.Name);
        await db.SaveChangesAsync(cancellationToken);
        return await UpdateCustomerHandler.SummaryAsync(db, links, customer.Id, now, cancellationToken);
    }
}

/// <summary>Turns the customer's portal link off. Tracking and invoice links already sent keep working.</summary>
[RequiresPolicy(Policies.Office)]
public sealed record DisablePortalLinkCommand(Guid CustomerId) : ICommand<CustomerDto>;

public sealed class DisablePortalLinkHandler(IAppDbContext db, IPublicLinks links, IAuditLog audit, TimeProvider clock)
    : ICommandHandler<DisablePortalLinkCommand, CustomerDto>
{
    public async ValueTask<CustomerDto> Handle(DisablePortalLinkCommand command, CancellationToken cancellationToken)
    {
        var customer = await CustomerDirectory.GetAsync(db, command.CustomerId, cancellationToken);
        customer.PortalToken = null;
        customer.PortalCreatedAtUtc = null;
        audit.Record("DisablePortalLink", "Customer", customer.Id.ToString(), customer.Name);
        await db.SaveChangesAsync(cancellationToken);
        return await UpdateCustomerHandler.SummaryAsync(db, links, customer.Id, clock.GetUtcNow().UtcDateTime, cancellationToken);
    }
}
