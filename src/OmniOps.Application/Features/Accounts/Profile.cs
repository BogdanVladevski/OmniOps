using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;
using OmniOps.Application.Common.Interfaces;
using OmniOps.Application.Common.Security;

namespace OmniOps.Application.Features.Accounts;

public sealed record UpdateProfileCommand(string DisplayName) : ICommand<AccountDto>, IRequirePolicy
{
    public string Policy => Policies.Office;
}

public sealed class UpdateProfileValidator : AbstractValidator<UpdateProfileCommand>
{
    public UpdateProfileValidator() => RuleFor(x => x.DisplayName).NotEmpty().WithMessage("Enter your name.").MaximumLength(200);
}

public sealed class UpdateProfileHandler(IAppDbContext db, ITenantContext tenant) : ICommandHandler<UpdateProfileCommand, AccountDto>
{
    public async ValueTask<AccountDto> Handle(UpdateProfileCommand command, CancellationToken cancellationToken)
    {
        var user = await db.SelfAsync(tenant, cancellationToken);
        user.DisplayName = command.DisplayName.Trim();
        await db.SaveChangesAsync(cancellationToken);
        return await user.ToDtoAsync(db, cancellationToken);
    }
}

/// <summary>Signs the caller out on every device, this one included.</summary>
public sealed record SignOutEverywhereCommand : ICommand, IRequirePolicy
{
    public string Policy => Policies.JobWork;
}

public sealed class SignOutEverywhereHandler(IAppDbContext db, IAccountSessions sessions, ITenantContext tenant)
    : ICommandHandler<SignOutEverywhereCommand>
{
    public async ValueTask<Unit> Handle(SignOutEverywhereCommand command, CancellationToken cancellationToken)
    {
        var user = await db.SelfAsync(tenant, cancellationToken);
        await sessions.RevokeAllAsync(user.Id, cancellationToken);
        return Unit.Value;
    }
}

public sealed record UpdateCompanyCommand(string Name) : ICommand<CompanyDto>, IRequirePolicy
{
    public string Policy => Policies.Owner;
}

public sealed class UpdateCompanyValidator : AbstractValidator<UpdateCompanyCommand>
{
    public UpdateCompanyValidator() => RuleFor(x => x.Name).NotEmpty().WithMessage("Enter your company's name.").MaximumLength(200);
}

public sealed class UpdateCompanyHandler(IAppDbContext db, ITenantContext tenant, IAuditLog audit) : ICommandHandler<UpdateCompanyCommand, CompanyDto>
{
    public async ValueTask<CompanyDto> Handle(UpdateCompanyCommand command, CancellationToken cancellationToken)
    {
        var company = await db.Organizations.FirstAsync(o => o.Id == tenant.OrganizationId, cancellationToken);
        company.Name = command.Name.Trim();
        audit.Record("UpdateCompany", "Organization", company.Id.ToString(), company.Name);
        await db.SaveChangesAsync(cancellationToken);
        return new CompanyDto(company.Id, company.Name);
    }
}
