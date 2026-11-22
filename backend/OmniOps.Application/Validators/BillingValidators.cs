using FluentValidation;
using OmniOps.Application.Commands;

namespace OmniOps.Application.Validators;

public class SaveBillingProfileCommandValidator : AbstractValidator<SaveBillingProfileCommand>
{
    public SaveBillingProfileCommandValidator()
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
        RuleFor(x => x.InvoicePrefix).NotEmpty().Matches("^[A-Za-z0-9]{1,12}$")
            .WithMessage("Use up to 12 letters or digits, e.g. INV.");
    }

    // Accepts IBANs with spaces; full checksum validation is the bank's job, this catches obvious typos.
    private static bool BeAnAccountNumber(string? value)
    {
        var compact = (value ?? string.Empty).Replace(" ", string.Empty);
        return compact.Length is >= 8 and <= 34 && compact.All(char.IsLetterOrDigit);
    }
}

public class CreateInvoiceCommandValidator : AbstractValidator<CreateInvoiceCommand>
{
    public CreateInvoiceCommandValidator()
    {
        RuleFor(x => x.JobId).NotEmpty();
        RuleFor(x => x.CustomerName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CustomerEmail).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.CustomerEmail))
            .WithMessage("That email address does not look right.").MaximumLength(200);
        RuleFor(x => x.CustomerAddress).MaximumLength(500);
        RuleFor(x => x.CustomerTaxId).MaximumLength(50);
        RuleFor(x => x.PaymentTermsDays).InclusiveBetween(0, 180).When(x => x.PaymentTermsDays.HasValue);
        RuleFor(x => x.Notes).MaximumLength(1000);
        RuleFor(x => x.Lines).NotNull().Must(l => l.Count is > 0 and <= CreateInvoiceCommand.MaxLines)
            .WithMessage("An invoice needs between 1 and 20 lines.");
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.Description).NotEmpty().MaximumLength(300);
            line.RuleFor(l => l.Quantity).GreaterThan(0).LessThanOrEqualTo(100_000);
            line.RuleFor(l => l.UnitPrice).GreaterThan(0).WithMessage("Enter the price.").LessThanOrEqualTo(10_000_000);
            line.RuleFor(l => l.VatRatePercent).InclusiveBetween(0, 50);
        });
    }
}
