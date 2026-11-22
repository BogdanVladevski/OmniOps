using MediatR;
using OmniOps.Application.Abstractions;
using OmniOps.Application.Dtos;
using OmniOps.Core.Authorization;

namespace OmniOps.Application.Commands;

public record SaveBillingProfileCommand(
    string LegalName,
    string Address,
    string? TaxId,
    string? Iban,
    string? BankName,
    string? Email,
    string? Phone,
    string Currency,
    decimal DefaultVatRatePercent,
    int DefaultPaymentTermsDays,
    string InvoicePrefix) : IRequest<BillingProfileDto>, IAuthorizedRequest
{
    public string RequiredPolicy => AuthorizationPolicies.FleetAdmin;
}

public record InvoiceLineInput(string Description, decimal Quantity, decimal UnitPrice, decimal VatRatePercent);

/// <summary>
/// Issues an invoice for a delivered job. Runs in one transaction with the number allocation, so a failure
/// gives the number back. Sending is a separate step (<see cref="SendInvoiceCommand"/>) that runs after commit.
/// </summary>
public record CreateInvoiceCommand(
    Guid JobId,
    string CustomerName,
    string? CustomerEmail,
    string? CustomerAddress,
    string? CustomerTaxId,
    int? PaymentTermsDays,
    IReadOnlyList<InvoiceLineInput> Lines,
    string? Notes) : IRequest<InvoiceDetailDto>, IAuthorizedRequest, ITransactionalRequest
{
    public const int MaxLines = 20;
    public string RequiredPolicy => AuthorizationPolicies.VehicleRead;
}

/// <summary>
/// Emails the invoice link to the customer (<paramref name="Email"/> = true) or just records that the office
/// sent it another way (WhatsApp, their own email). Either way the invoice becomes "Sent" and reminders start.
/// </summary>
public record SendInvoiceCommand(Guid InvoiceId, bool Email) : IRequest<InvoiceDetailDto>, IAuthorizedRequest
{
    public string RequiredPolicy => AuthorizationPolicies.VehicleRead;
}

public record MarkInvoicePaidCommand(Guid InvoiceId, DateTimeOffset? PaidAt) : IRequest<InvoiceDetailDto>, IAuthorizedRequest
{
    public string RequiredPolicy => AuthorizationPolicies.VehicleRead;
}

/// <summary>System job: sends the payment reminders that are due across all organizations.</summary>
public record ProcessInvoiceRemindersCommand : IRequest<InvoiceReminderRunDto>;
