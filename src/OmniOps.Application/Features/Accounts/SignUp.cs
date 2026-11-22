using System.Security.Cryptography;
using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OmniOps.Application.Common.Interfaces;
using OmniOps.Domain.Accounts;
using OmniOps.Domain.Common;

namespace OmniOps.Application.Features.Accounts;

/// <summary>Anonymous: creates a company account and its first owner.</summary>
public sealed record SignUpCommand(string CompanyName, string Name, string Email, string Password) : ICommand<AccountDto>;

public sealed class SignUpValidator : AbstractValidator<SignUpCommand>
{
    public SignUpValidator()
    {
        RuleFor(x => x.CompanyName).NotEmpty().WithMessage("Enter your company's name.").MaximumLength(200);
        RuleFor(x => x.Name).NotEmpty().WithMessage("Enter your name.").MaximumLength(200);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().WithMessage("Enter a valid email address.").MaximumLength(200);
        RuleFor(x => x.Password).NewPassword();
    }
}

public sealed class SignUpHandler(IAppDbContext db, IPasswordHasher hasher, TimeProvider clock, ILogger<SignUpHandler> logger)
    : ICommandHandler<SignUpCommand, AccountDto>
{
    public async ValueTask<AccountDto> Handle(SignUpCommand command, CancellationToken cancellationToken)
    {
        var email = AccountRules.NormalizeEmail(command.Email);
        if (await db.Users.AcrossCompanies().AnyAsync(u => u.Email == email, cancellationToken))
        {
            throw new BusinessRuleException("An account with this email already exists. Sign in instead.");
        }

        var slug = AccountRules.Slugify(command.CompanyName);
        while (await db.Organizations.AnyAsync(o => o.Slug == slug, cancellationToken))
        {
            slug = $"{AccountRules.Slugify(command.CompanyName)}-{RandomNumberGenerator.GetInt32(1000, 9999)}";
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var company = new Organization { Id = Guid.NewGuid(), Name = command.CompanyName.Trim(), Slug = slug, CreatedAtUtc = now };
        var owner = new UserAccount
        {
            Id = Guid.NewGuid(),
            OrganizationId = company.Id,
            Email = email,
            DisplayName = command.Name.Trim(),
            PasswordHash = hasher.Hash(command.Password),
            Role = UserRole.Owner,
            CreatedAtUtc = now,
            LastSignInAtUtc = now,
        };

        db.Organizations.Add(company);
        db.Users.Add(owner);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("New company account {OrganizationId} ({Slug})", company.Id, slug);

        return new AccountDto(owner.Id, owner.DisplayName, owner.Email, owner.Role.ToString(), company.Id, company.Name, null);
    }
}

internal static class PasswordRules
{
    public static IRuleBuilderOptions<T, string> NewPassword<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MinimumLength(AccountRules.MinPasswordLength)
            .WithMessage($"Use at least {AccountRules.MinPasswordLength} characters for your password.").MaximumLength(200);
}
