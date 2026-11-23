using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;
using OmniOps.Application.Common.Interfaces;
using OmniOps.Application.Common.Security;
using OmniOps.Application.Features.Jobs;
using OmniOps.Domain.Billing;

namespace OmniOps.Application.Features.Billing;

[RequiresPolicy(Policies.Office)]
public sealed record GetBillingProfileQuery : IQuery<BillingProfileDto>;

public sealed class GetBillingProfileHandler(IAppDbContext db, IEmailSender email) : IQueryHandler<GetBillingProfileQuery, BillingProfileDto>
{
    public async ValueTask<BillingProfileDto> Handle(GetBillingProfileQuery query, CancellationToken cancellationToken) =>
        BillingProfileDto.From(await db.BillingProfiles.FirstOrDefaultAsync(cancellationToken), email.IsConfigured);
}

/// <summary>The company's invoice details, terms, numbering and waiting-time terms.</summary>
[RequiresPolicy(Policies.Owner)]
public sealed record SaveBillingProfileCommand(
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
    string InvoicePrefix,
    int FreeWaitingMinutes = 120,
    decimal? WaitingRatePerHour = null,
    string? TimeZone = null) : ICommand<BillingProfileDto>;

public sealed class SaveBillingProfileValidator : AbstractValidator<SaveBillingProfileCommand>
{
    public SaveBillingProfileValidator()
    {
        RuleFor(x => x.LegalName).NotEmpty().WithMessage("Enter your company's legal name.").MaximumLength(200);
        RuleFor(x => x.Address).NotEmpty().WithMessage("Enter your company's address.").MaximumLength(500);
        RuleFor(x => x.TaxId).MaximumLength(50);
        RuleFor(x => x.Iban).NotEmpty().WithMessage("Enter the bank account customers should pay into.")
            .Must(BeAnAccountNumber).WithMessage("That does not look like a bank account number (IBAN).");
        RuleFor(x => x.BankName).MaximumLength(120);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email)).MaximumLength(200);
        RuleFor(x => x.Phone).MaximumLength(50);
        RuleFor(x => x.Currency).Matches("^[A-Z]{3}$").WithMessage("Use a 3-letter currency code such as EUR or MKD.");
        RuleFor(x => x.DefaultVatRatePercent).InclusiveBetween(0, 50);
        RuleFor(x => x.DefaultPaymentTermsDays).InclusiveBetween(0, 180);
        RuleFor(x => x.FreeWaitingMinutes).InclusiveBetween(0, 24 * 60).WithMessage("Free waiting time must be between 0 and 24 hours.");
        RuleFor(x => x.WaitingRatePerHour).InclusiveBetween(0, 10_000).When(x => x.WaitingRatePerHour.HasValue);
        RuleFor(x => x.TimeZone).MaximumLength(64);
        RuleFor(x => x.InvoicePrefix).NotEmpty().Matches("^[A-Za-z0-9]{1,12}$").WithMessage("Use up to 12 letters or digits, e.g. INV.");
    }

    // Accepts IBANs with spaces; full checksum validation is the bank's job, this catches obvious typos.
    private static bool BeAnAccountNumber(string? value)
    {
        var compact = (value ?? string.Empty).Replace(" ", string.Empty, StringComparison.Ordinal);
        return compact.Length is >= 8 and <= 34 && compact.All(char.IsLetterOrDigit);
    }
}

public sealed class SaveBillingProfileHandler(IAppDbContext db, ITenantContext tenant, IEmailSender email, IAuditLog audit, TimeProvider clock)
    : ICommandHandler<SaveBillingProfileCommand, BillingProfileDto>
{
    public async ValueTask<BillingProfileDto> Handle(SaveBillingProfileCommand command, CancellationToken cancellationToken)
    {
        var profile = await db.BillingProfiles.FirstOrDefaultAsync(cancellationToken);
        if (profile is null)
        {
            profile = new BillingProfile { OrganizationId = tenant.OrganizationId };
            db.BillingProfiles.Add(profile);
        }

        // NextInvoiceSequence is deliberately never touched here: only issuing an invoice advances it.
        profile.LegalName = command.LegalName.Trim();
        profile.Address = command.Address.Trim();
        profile.TaxId = BillingText.Clean(command.TaxId);
        profile.Iban = BillingText.Clean(command.Iban)?.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
        profile.BankName = BillingText.Clean(command.BankName);
        profile.Email = BillingText.Clean(command.Email);
        profile.Phone = BillingText.Clean(command.Phone);
        profile.Currency = command.Currency.ToUpperInvariant();
        profile.DefaultVatRatePercent = command.DefaultVatRatePercent;
        profile.DefaultPaymentTermsDays = command.DefaultPaymentTermsDays;
        profile.InvoicePrefix = command.InvoicePrefix.Trim().ToUpperInvariant();
        profile.FreeWaitingMinutes = command.FreeWaitingMinutes;
        profile.WaitingRatePerHour = command.WaitingRatePerHour is > 0 ? command.WaitingRatePerHour : null;
        if (WaitingTerms.ZoneOf(command.TimeZone) != TimeZoneInfo.Utc)
        {
            profile.TimeZone = command.TimeZone!.Trim();
        }

        profile.UpdatedAtUtc = clock.GetUtcNow().UtcDateTime;
        audit.Record("Update", "BillingProfile", profile.OrganizationId.ToString());
        await db.SaveChangesAsync(cancellationToken);
        return BillingProfileDto.From(profile, email.IsConfigured);
    }
}
