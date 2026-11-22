using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;
using OmniOps.Application.Common.Interfaces;
using OmniOps.Domain.Accounts;
using OmniOps.Domain.Common;

namespace OmniOps.Application.Features.Accounts;

/// <summary>Anonymous: what the "join company" screen shows. Not found when used, withdrawn or expired.</summary>
public sealed record GetInvitationQuery(string Token) : IQuery<InvitationPreviewDto>;

public sealed class GetInvitationHandler(IAppDbContext db, TimeProvider clock) : IQueryHandler<GetInvitationQuery, InvitationPreviewDto>
{
    public async ValueTask<InvitationPreviewDto> Handle(GetInvitationQuery query, CancellationToken cancellationToken)
    {
        var link = await db.OpenLinkAsync(query.Token, AccountLinkPurpose.Invitation, clock.GetUtcNow().UtcDateTime, cancellationToken);
        var company = await db.Organizations.Where(o => o.Id == link.OrganizationId).Select(o => o.Name).FirstAsync(cancellationToken);
        return new InvitationPreviewDto(company, link.Email, link.Role.ToString());
    }
}

/// <summary>Anonymous: creates the invited person's account and signs them in.</summary>
public sealed record AcceptInvitationCommand(string Token, string Name, string Password) : ICommand<AccountDto>;

public sealed class AcceptInvitationValidator : AbstractValidator<AcceptInvitationCommand>
{
    public AcceptInvitationValidator()
    {
        RuleFor(x => x.Token).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Name).NotEmpty().WithMessage("Enter your name.").MaximumLength(200);
        RuleFor(x => x.Password).NewPassword();
    }
}

public sealed class AcceptInvitationHandler(IAppDbContext db, IPasswordHasher hasher, IAuditLog audit, TimeProvider clock)
    : ICommandHandler<AcceptInvitationCommand, AccountDto>
{
    public async ValueTask<AccountDto> Handle(AcceptInvitationCommand command, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var link = await db.OpenLinkAsync(command.Token, AccountLinkPurpose.Invitation, now, cancellationToken);
        if (await db.Users.AcrossCompanies().AnyAsync(u => u.Email == link.Email, cancellationToken))
        {
            throw new BusinessRuleException("This email already has an OmniOps account. Sign in instead.");
        }

        var user = new UserAccount
        {
            Id = Guid.NewGuid(),
            OrganizationId = link.OrganizationId,
            Email = link.Email,
            DisplayName = command.Name.Trim(),
            PasswordHash = hasher.Hash(command.Password),
            Role = link.Role,
            CreatedAtUtc = now,
            LastSignInAtUtc = now,
        };
        link.UsedAtUtc = now;
        db.Users.Add(user);
        audit.Record(link.OrganizationId, user.Id, "AcceptInvitation", "UserAccount", user.Id.ToString(), link.Email);
        await db.SaveChangesAsync(cancellationToken);
        return await user.ToDtoAsync(db, cancellationToken);
    }
}

internal static class AccountLinks
{
    /// <summary>A link that can still be used, found by its token across companies (the token is the proof).</summary>
    public static async Task<AccountLink> OpenLinkAsync(
        this IAppDbContext db, string token, AccountLinkPurpose purpose, DateTime now, CancellationToken ct)
    {
        var hash = string.IsNullOrWhiteSpace(token) ? null : AccountRules.Hash(token.Trim());
        var link = hash is null ? null : await db.AccountLinks.AcrossCompanies().FirstOrDefaultAsync(l => l.TokenHash == hash, ct);
        if (link is null || link.Purpose != purpose || !link.IsOpen(now))
        {
            throw new KeyNotFoundException(purpose == AccountLinkPurpose.Invitation
                ? "This invitation has expired or was already used. Ask for a new one."
                : "This link has expired or was already used. Ask for a new one.");
        }

        return link;
    }

    public static AccountLink NewPasswordReset(UserAccount user, string token, Guid? createdBy, DateTime now) => new()
    {
        Id = Guid.NewGuid(),
        OrganizationId = user.OrganizationId,
        Purpose = AccountLinkPurpose.PasswordReset,
        Email = user.Email ?? string.Empty,
        Role = user.Role,
        UserId = user.Id,
        TokenHash = AccountRules.Hash(token),
        ExpiresAtUtc = now + AccountRules.PasswordResetLifetime,
        CreatedByUserId = createdBy?.ToString(),
        CreatedAtUtc = now,
    };
}
