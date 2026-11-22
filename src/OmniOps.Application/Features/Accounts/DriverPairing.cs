using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;
using OmniOps.Application.Common.Interfaces;
using OmniOps.Application.Common.Security;
using OmniOps.Domain.Accounts;
using OmniOps.Domain.Common;

namespace OmniOps.Application.Features.Accounts;

/// <summary>Office: a one-time code a driver types to sign in their phone (valid 24 hours).</summary>
public sealed record CreateDriverPairingCodeCommand(Guid DriverId) : ICommand<DriverPairingCodeDto>, IRequirePolicy
{
    public string Policy => Policies.Office;
}

public sealed class CreateDriverPairingCodeHandler(IAppDbContext db, ITenantContext tenant, IAuditLog audit, TimeProvider clock)
    : ICommandHandler<CreateDriverPairingCodeCommand, DriverPairingCodeDto>
{
    public async ValueTask<DriverPairingCodeDto> Handle(CreateDriverPairingCodeCommand command, CancellationToken cancellationToken)
    {
        var driver = await db.Drivers.FirstOrDefaultAsync(d => d.Id == command.DriverId, cancellationToken)
            ?? throw new KeyNotFoundException("Driver not found.");

        var now = clock.GetUtcNow().UtcDateTime;
        var code = AccountRules.NewPairingCode();
        db.DriverPairingCodes.Add(new DriverPairingCode
        {
            Id = Guid.NewGuid(),
            OrganizationId = tenant.OrganizationId,
            DriverId = driver.Id,
            CodeHash = AccountRules.Hash(AccountRules.NormalizeCode(code)),
            ExpiresAtUtc = now + AccountRules.PairingCodeLifetime,
            CreatedByUserId = tenant.UserId?.ToString(),
            CreatedAtUtc = now,
        });
        audit.Record("CreatePairingCode", "Driver", driver.Id.ToString());
        await db.SaveChangesAsync(cancellationToken);
        return new DriverPairingCodeDto(code, now + AccountRules.PairingCodeLifetime, driver.FullName);
    }
}

/// <summary>Anonymous: a driver's phone exchanges a pairing code for a driver sign-in. Each code works once.</summary>
public sealed record PairDriverCommand(string Code) : ICommand<AccountDto>;

public sealed class PairDriverValidator : AbstractValidator<PairDriverCommand>
{
    public PairDriverValidator() => RuleFor(x => x.Code).NotEmpty().WithMessage("Enter the code from the office.").MaximumLength(20);
}

public sealed class PairDriverHandler(IAppDbContext db, TimeProvider clock) : ICommandHandler<PairDriverCommand, AccountDto>
{
    private const string Invalid = "That code is not valid or has expired. Ask the office for a new one.";

    public async ValueTask<AccountDto> Handle(PairDriverCommand command, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var hash = AccountRules.Hash(AccountRules.NormalizeCode(command.Code));
        var pairing = await db.DriverPairingCodes.AcrossCompanies().FirstOrDefaultAsync(c => c.CodeHash == hash, cancellationToken);
        if (pairing is null || pairing.UsedAtUtc is not null || pairing.ExpiresAtUtc <= now)
        {
            throw new AuthenticationFailedException(Invalid);
        }

        var driver = await db.Drivers.AcrossCompanies()
            .FirstOrDefaultAsync(d => d.Id == pairing.DriverId && d.OrganizationId == pairing.OrganizationId && d.IsActive, cancellationToken)
            ?? throw new AuthenticationFailedException(Invalid);

        pairing.UsedAtUtc = now;
        var account = await db.Users.AcrossCompanies()
            .FirstOrDefaultAsync(u => u.OrganizationId == pairing.OrganizationId && u.DriverId == driver.Id, cancellationToken);
        if (account is null)
        {
            account = new UserAccount
            {
                Id = Guid.NewGuid(),
                OrganizationId = pairing.OrganizationId,
                Role = UserRole.Driver,
                DriverId = driver.Id,
                CreatedAtUtc = now,
            };
            db.Users.Add(account);
        }

        account.IsActive = true;
        account.DisplayName = driver.FullName;
        account.LastSignInAtUtc = now;
        await db.SaveChangesAsync(cancellationToken);
        return await account.ToDtoAsync(db, cancellationToken);
    }
}
