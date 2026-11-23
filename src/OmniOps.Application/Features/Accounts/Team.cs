using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OmniOps.Application.Common.Interfaces;
using OmniOps.Application.Common.Security;
using OmniOps.Domain.Accounts;
using OmniOps.Domain.Common;

namespace OmniOps.Application.Features.Accounts;

// Owners invite office staff and manage who can sign in. Drivers join with pairing codes instead.

[RequiresPolicy(Policies.Owner)]
public sealed record GetTeamQuery : IQuery<TeamDto>;

public sealed class GetTeamHandler(IAppDbContext db, ITenantContext tenant, IEmailSender email, TimeProvider clock)
    : IQueryHandler<GetTeamQuery, TeamDto>
{
    public async ValueTask<TeamDto> Handle(GetTeamQuery query, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var members = await db.Users.Where(u => u.Role != UserRole.Driver)
            .OrderBy(u => u.Role).ThenBy(u => u.DisplayName).ToListAsync(cancellationToken);
        var invitations = await db.AccountLinks.OpenInvitations(now).OrderByDescending(l => l.CreatedAtUtc).ToListAsync(cancellationToken);
        return new TeamDto(
            members.Select(m => m.ToMember(tenant)).ToList(),
            invitations.Select(i => new TeamInvitationDto(i.Id, i.Email, i.Role.ToString(), i.ExpiresAtUtc, null, false)).ToList(),
            email.IsConfigured);
    }
}

/// <summary>Role is "Owner" or "Dispatcher". The link is emailed when possible and always returned to share by hand.</summary>
[RequiresPolicy(Policies.Owner)]
public sealed record InviteTeammateCommand(string Email, string Role) : ICommand<TeamInvitationDto>;

public sealed class InviteTeammateValidator : AbstractValidator<InviteTeammateCommand>
{
    public InviteTeammateValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().WithMessage("Enter a valid email address.").MaximumLength(200);
        RuleFor(x => x.Role).Must(AccountRules.IsOfficeRole).WithMessage("Choose owner or dispatcher.");
    }
}

public sealed class InviteTeammateHandler(
    IAppDbContext db, ITenantContext tenant, IEmailSender email, IPublicLinks links, IAuditLog audit, TimeProvider clock,
    ILogger<InviteTeammateHandler> logger) : ICommandHandler<InviteTeammateCommand, TeamInvitationDto>
{
    public async ValueTask<TeamInvitationDto> Handle(InviteTeammateCommand command, CancellationToken cancellationToken)
    {
        var address = AccountRules.NormalizeEmail(command.Email);
        var role = Enum.Parse<UserRole>(command.Role, ignoreCase: true);
        if (await db.Users.AcrossCompanies().AnyAsync(u => u.Email == address, cancellationToken))
        {
            throw new BusinessRuleException("This email already has an OmniOps account. Use a different email.");
        }

        var now = clock.GetUtcNow().UtcDateTime;
        // Only the latest invitation to the same person works.
        await foreach (var earlier in db.AccountLinks.OpenInvitations(now).Where(l => l.Email == address).AsAsyncEnumerable().WithCancellation(cancellationToken))
        {
            earlier.RevokedAtUtc = now;
        }

        var token = AccountRules.NewLinkToken();
        var invitation = new AccountLink
        {
            Id = Guid.NewGuid(),
            OrganizationId = tenant.OrganizationId,
            Purpose = AccountLinkPurpose.Invitation,
            Email = address,
            Role = role,
            TokenHash = AccountRules.Hash(token),
            ExpiresAtUtc = now + AccountRules.InvitationLifetime,
            CreatedByUserId = tenant.UserId?.ToString(),
            CreatedAtUtc = now,
        };
        db.AccountLinks.Add(invitation);
        audit.Record("InviteTeammate", "AccountLink", invitation.Id.ToString(), $"{address} as {role}");
        await db.SaveChangesAsync(cancellationToken);

        var link = links.Join(token);
        var company = await db.Organizations.Where(o => o.Id == tenant.OrganizationId).Select(o => o.Name).FirstAsync(cancellationToken);
        var inviter = await db.Users.Where(u => u.Id == tenant.UserId).Select(u => u.DisplayName).FirstOrDefaultAsync(cancellationToken) ?? company;
        var emailed = await email.TrySendAsync(logger, address, AccountEmails.Invitation(company, inviter, role, link), cancellationToken);
        return new TeamInvitationDto(invitation.Id, address, role.ToString(), invitation.ExpiresAtUtc, link, emailed);
    }
}

[RequiresPolicy(Policies.Owner)]
public sealed record RevokeInvitationCommand(Guid InvitationId) : ICommand;

public sealed class RevokeInvitationHandler(IAppDbContext db, IAuditLog audit, TimeProvider clock) : ICommandHandler<RevokeInvitationCommand>
{
    public async ValueTask<Unit> Handle(RevokeInvitationCommand command, CancellationToken cancellationToken)
    {
        var link = await db.AccountLinks.FirstOrDefaultAsync(
                l => l.Id == command.InvitationId && l.Purpose == AccountLinkPurpose.Invitation, cancellationToken)
            ?? throw new KeyNotFoundException("Invitation not found.");
        link.RevokedAtUtc ??= clock.GetUtcNow().UtcDateTime;
        audit.Record("RevokeInvitation", "AccountLink", link.Id.ToString(), link.Email);
        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

[RequiresPolicy(Policies.Owner)]
public sealed record ChangeTeammateRoleCommand(Guid UserId, string Role) : ICommand<TeamMemberDto>;

public sealed class ChangeTeammateRoleValidator : AbstractValidator<ChangeTeammateRoleCommand>
{
    public ChangeTeammateRoleValidator() => RuleFor(x => x.Role).Must(AccountRules.IsOfficeRole).WithMessage("Choose owner or dispatcher.");
}

public sealed class ChangeTeammateRoleHandler(IAppDbContext db, IAccountSessions sessions, ITenantContext tenant, IAuditLog audit)
    : ICommandHandler<ChangeTeammateRoleCommand, TeamMemberDto>
{
    public async ValueTask<TeamMemberDto> Handle(ChangeTeammateRoleCommand command, CancellationToken cancellationToken)
    {
        var user = await db.OfficeMemberAsync(command.UserId, cancellationToken);
        var role = Enum.Parse<UserRole>(command.Role, ignoreCase: true);
        if (user.Role == role)
        {
            return user.ToMember(tenant);
        }

        if (user is { Role: UserRole.Owner, IsActive: true } && await db.ActiveOwnersAsync(cancellationToken) <= 1)
        {
            throw new BusinessRuleException("A company needs at least one owner. Make someone else an owner first.");
        }

        user.Role = role;
        audit.Record("ChangeTeammateRole", "UserAccount", user.Id.ToString(), role.ToString());
        await db.SaveChangesAsync(cancellationToken);
        // What they may do is part of their sign-in, so they sign in again with the new role.
        await sessions.RevokeAllAsync(user.Id, cancellationToken);
        return user.ToMember(tenant);
    }
}

/// <summary>Switches the account off and ends its sign-ins. Its history (jobs, audit log) stays.</summary>
[RequiresPolicy(Policies.Owner)]
public sealed record RemoveTeammateCommand(Guid UserId) : ICommand;

public sealed class RemoveTeammateHandler(IAppDbContext db, IAccountSessions sessions, ITenantContext tenant, IAuditLog audit)
    : ICommandHandler<RemoveTeammateCommand>
{
    public async ValueTask<Unit> Handle(RemoveTeammateCommand command, CancellationToken cancellationToken)
    {
        var user = await db.OfficeMemberAsync(command.UserId, cancellationToken);
        if (user.Id == tenant.UserId)
        {
            throw new BusinessRuleException("You can't remove yourself. Ask another owner.");
        }

        if (user is { Role: UserRole.Owner, IsActive: true } && await db.ActiveOwnersAsync(cancellationToken) <= 1)
        {
            throw new BusinessRuleException("A company needs at least one owner.");
        }

        user.IsActive = false;
        audit.Record("RemoveTeammate", "UserAccount", user.Id.ToString(), user.Email);
        await db.SaveChangesAsync(cancellationToken);
        await sessions.RevokeAllAsync(user.Id, cancellationToken);
        return Unit.Value;
    }
}

internal static class TeamQueries
{
    public static IQueryable<AccountLink> OpenInvitations(this IQueryable<AccountLink> links, DateTime now) =>
        links.Where(l => l.Purpose == AccountLinkPurpose.Invitation && l.UsedAtUtc == null && l.RevokedAtUtc == null && l.ExpiresAtUtc > now);

    /// <summary>An owner or dispatcher of the caller's company; anyone else (another company, a driver) is "not found".</summary>
    public static async Task<UserAccount> OfficeMemberAsync(this IAppDbContext db, Guid userId, CancellationToken ct) =>
        await db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.Role != UserRole.Driver, ct)
        ?? throw new KeyNotFoundException("That person is not in your team.");

    public static Task<int> ActiveOwnersAsync(this IAppDbContext db, CancellationToken ct) =>
        db.Users.CountAsync(u => u.Role == UserRole.Owner && u.IsActive, ct);

    public static TeamMemberDto ToMember(this UserAccount user, ITenantContext tenant) =>
        new(user.Id, user.DisplayName, user.Email, user.Role.ToString(), user.IsActive, user.LastSignInAtUtc, user.Id == tenant.UserId);
}
