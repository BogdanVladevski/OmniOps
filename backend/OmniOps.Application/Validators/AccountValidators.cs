using FluentValidation;
using OmniOps.Application.Commands;

namespace OmniOps.Application.Validators;

public class SignUpCommandValidator : AbstractValidator<SignUpCommand>
{
    public const int MinPasswordLength = 10;

    public SignUpCommandValidator()
    {
        RuleFor(x => x.CompanyName).NotEmpty().WithMessage("Enter your company's name.").MaximumLength(200);
        RuleFor(x => x.Name).NotEmpty().WithMessage("Enter your name.").MaximumLength(200);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().WithMessage("Enter a valid email address.").MaximumLength(200);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(MinPasswordLength)
            .WithMessage($"Use at least {MinPasswordLength} characters for your password.").MaximumLength(200);
    }
}

public class SignInCommandValidator : AbstractValidator<SignInCommand>
{
    public SignInCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(200);
    }
}

public class PairDriverCommandValidator : AbstractValidator<PairDriverCommand>
{
    public PairDriverCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().WithMessage("Enter the code from the office.").MaximumLength(20);
    }
}
