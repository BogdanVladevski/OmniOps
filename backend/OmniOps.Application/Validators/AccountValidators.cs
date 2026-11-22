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

internal static class PasswordRule
{
    public static IRuleBuilderOptions<T, string> NewPassword<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MinimumLength(SignUpCommandValidator.MinPasswordLength)
            .WithMessage($"Use at least {SignUpCommandValidator.MinPasswordLength} characters for your password.").MaximumLength(200);

    public static bool IsOfficeRole(string role) =>
        string.Equals(role, "Owner", StringComparison.OrdinalIgnoreCase) || string.Equals(role, "Dispatcher", StringComparison.OrdinalIgnoreCase);
}

public class InviteTeammateCommandValidator : AbstractValidator<InviteTeammateCommand>
{
    public InviteTeammateCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().WithMessage("Enter a valid email address.").MaximumLength(200);
        RuleFor(x => x.Role).Must(PasswordRule.IsOfficeRole).WithMessage("Choose owner or dispatcher.");
    }
}

public class ChangeTeammateRoleCommandValidator : AbstractValidator<ChangeTeammateRoleCommand>
{
    public ChangeTeammateRoleCommandValidator()
    {
        RuleFor(x => x.Role).Must(PasswordRule.IsOfficeRole).WithMessage("Choose owner or dispatcher.");
    }
}

public class AcceptInvitationCommandValidator : AbstractValidator<AcceptInvitationCommand>
{
    public AcceptInvitationCommandValidator()
    {
        RuleFor(x => x.Token).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Name).NotEmpty().WithMessage("Enter your name.").MaximumLength(200);
        RuleFor(x => x.Password).NewPassword();
    }
}

public class RequestPasswordResetCommandValidator : AbstractValidator<RequestPasswordResetCommand>
{
    public RequestPasswordResetCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().WithMessage("Enter your email.").MaximumLength(200);
    }
}

public class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(x => x.Token).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Password).NewPassword();
    }
}

public class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty().WithMessage("Enter your current password.").MaximumLength(200);
        RuleFor(x => x.NewPassword).NewPassword();
    }
}

public class UpdateProfileCommandValidator : AbstractValidator<UpdateProfileCommand>
{
    public UpdateProfileCommandValidator()
    {
        RuleFor(x => x.DisplayName).NotEmpty().WithMessage("Enter your name.").MaximumLength(200);
    }
}

public class UpdateCompanyCommandValidator : AbstractValidator<UpdateCompanyCommand>
{
    public UpdateCompanyCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Enter your company's name.").MaximumLength(200);
    }
}
