using MediatR;
using Microsoft.Extensions.Logging;
using OmniOps.Application.Dtos;
using OmniOps.Core.Entities;
using OmniOps.Core.Enums;
using OmniOps.Core.Exceptions;
using OmniOps.Core.Interfaces;

namespace OmniOps.Application.Commands.Handlers;

internal static class TeamText
{
    public static string RoleName(UserRole role) => role == UserRole.Owner ? "owner" : "dispatcher";

    public static (string Subject, string Body) Invitation(string company, string inviter, UserRole role, string link) => (
        $"{inviter} invited you to {company} on OmniOps",
        $"""
        Hello,

        {inviter} invited you to join {company} on OmniOps as {(role == UserRole.Owner ? "an owner" : "a dispatcher")}.

        Accept the invitation and choose your password here (the link works for 7 days):
        {link}

        If you did not expect this, you can ignore this email.
        """);

    public static (string Subject, string Body) PasswordReset(string name, string link) => (
        "Choose a new OmniOps password",
        $"""
        Hello {name},

        Use this link to choose a new password. It works once, for one hour:
        {link}

        If you did not ask for this, ignore this email: your password stays the same.
        """);
}

/// <summary>Shared steps for the team handlers.</summary>
internal static class TeamSupport
{
    public static async Task<UserAccount> OfficeMemberAsync(IAccountRepository accounts, ITenantContext tenant, Guid userId, CancellationToken ct)
    {
        var user = await accounts.GetByIdAsync(userId, ct);
        // Another company's account (or a driver) is simply not found.
        if (user is null || user.OrganizationId != tenant.OrganizationId || user.Role == UserRole.Driver)
        {
            throw new KeyNotFoundException("That person is not in your team.");
        }

        return user;
    }

    public static TeamMemberDto ToDto(UserAccount user, ITenantContext tenant) =>
        new(user.Id, user.DisplayName, user.Email, user.Role.ToString(), user.IsActive, user.LastSignInAtUtc, user.Id.ToString() == tenant.UserId);

    public static async Task<bool> TrySendAsync(IEmailSender email, ILogger logger, string to, (string Subject, string Body) message, CancellationToken ct)
    {
        if (!email.IsConfigured)
        {
            return false;
        }

        try
        {
            await email.SendAsync(to, message.Subject, message.Body, ct);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not send {Subject}", message.Subject);
            return false;
        }
    }

    public static async Task<(AccountLink Link, string Token)> NewResetLinkAsync(
        IAccountRepository accounts, UserAccount user, string? createdBy, DateTime now, CancellationToken ct)
    {
        var token = AccountRules.NewLinkToken();
        var link = new AccountLink
        {
            Id = Guid.NewGuid(),
            OrganizationId = user.OrganizationId,
            Purpose = AccountLinkPurpose.PasswordReset,
            Email = user.Email ?? string.Empty,
            Role = user.Role,
            UserId = user.Id,
            TokenHash = AccountRules.HashCode(token),
            ExpiresAtUtc = now + AccountRules.PasswordResetLifetime,
            CreatedByUserId = createdBy,
            CreatedAtUtc = now,
        };
        await accounts.AddLinkAsync(link, ct);
        await accounts.SaveChangesAsync(ct);
        return (link, token);
    }

    public static async Task<AccountLink> OpenLinkAsync(IAccountRepository accounts, string token, AccountLinkPurpose purpose, DateTime now, CancellationToken ct)
    {
        var link = string.IsNullOrWhiteSpace(token) ? null : await accounts.GetLinkAsync(AccountRules.HashCode(token.Trim()), ct);
        if (link is null || link.Purpose != purpose || !link.IsOpen(now))
        {
            throw new KeyNotFoundException(purpose == AccountLinkPurpose.Invitation
                ? "This invitation has expired or was already used. Ask for a new one."
                : "This link has expired or was already used. Ask for a new one.");
        }

        return link;
    }
}

public class GetTeamQueryHandler : IRequestHandler<GetTeamQuery, TeamOverviewDto>
{
    private readonly IAccountRepository _accounts;
    private readonly ITenantContext _tenant;
    private readonly IEmailSender _email;
    private readonly TimeProvider _clock;

    public GetTeamQueryHandler(IAccountRepository accounts, ITenantContext tenant, IEmailSender email, TimeProvider clock)
    {
        _accounts = accounts;
        _tenant = tenant;
        _email = email;
        _clock = clock;
    }

    public async Task<TeamOverviewDto> Handle(GetTeamQuery request, CancellationToken cancellationToken)
    {
        var members = await _accounts.ListOfficeAsync(_tenant.OrganizationId, cancellationToken);
        var invitations = await _accounts.ListOpenInvitationsAsync(_tenant.OrganizationId, _clock.GetUtcNow().UtcDateTime, cancellationToken);
        return new TeamOverviewDto(
            members.Select(m => TeamSupport.ToDto(m, _tenant)).ToList(),
            invitations.Select(i => new TeamInvitationDto(i.Id, i.Email, i.Role.ToString(), i.ExpiresAtUtc, null, false)).ToList(),
            _email.IsConfigured);
    }
}

public class InviteTeammateCommandHandler : IRequestHandler<InviteTeammateCommand, TeamInvitationDto>
{
    private readonly IAccountRepository _accounts;
    private readonly ITenantContext _tenant;
    private readonly IEmailSender _email;
    private readonly IPublicLinks _links;
    private readonly IAuditService _audit;
    private readonly TimeProvider _clock;
    private readonly ILogger<InviteTeammateCommandHandler> _logger;

    public InviteTeammateCommandHandler(
        IAccountRepository accounts, ITenantContext tenant, IEmailSender email, IPublicLinks links, IAuditService audit,
        TimeProvider clock, ILogger<InviteTeammateCommandHandler> logger)
    {
        _accounts = accounts;
        _tenant = tenant;
        _email = email;
        _links = links;
        _audit = audit;
        _clock = clock;
        _logger = logger;
    }

    public async Task<TeamInvitationDto> Handle(InviteTeammateCommand request, CancellationToken cancellationToken)
    {
        var email = AccountRules.NormalizeEmail(request.Email);
        var role = Enum.Parse<UserRole>(request.Role, ignoreCase: true);
        if (await _accounts.GetByEmailAsync(email, cancellationToken) is not null)
        {
            throw new BusinessRuleException("This email already has an OmniOps account. Use a different email.");
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        // A new invitation replaces an earlier one to the same person: only the latest link works.
        foreach (var earlier in await _accounts.ListOpenInvitationsAsync(_tenant.OrganizationId, now, cancellationToken))
        {
            if (earlier.Email == email)
            {
                earlier.RevokedAtUtc = now;
            }
        }

        var token = AccountRules.NewLinkToken();
        var invitation = new AccountLink
        {
            Id = Guid.NewGuid(),
            OrganizationId = _tenant.OrganizationId,
            Purpose = AccountLinkPurpose.Invitation,
            Email = email,
            Role = role,
            TokenHash = AccountRules.HashCode(token),
            ExpiresAtUtc = now + AccountRules.InvitationLifetime,
            CreatedByUserId = _tenant.UserId,
            CreatedAtUtc = now,
        };
        await _accounts.AddLinkAsync(invitation, cancellationToken);
        await _accounts.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync("InviteTeammate", "AccountLink", invitation.Id.ToString(), _tenant.UserId, $"{email} as {role}", cancellationToken);

        var link = _links.Join(token);
        var company = await _accounts.OrganizationNameAsync(_tenant.OrganizationId, cancellationToken) ?? "your company";
        var inviter = Guid.TryParse(_tenant.UserId, out var me) && await _accounts.GetByIdAsync(me, cancellationToken) is { } u
            ? u.DisplayName
            : company;
        var emailed = await TeamSupport.TrySendAsync(_email, _logger, email, TeamText.Invitation(company, inviter, role, link), cancellationToken);
        return new TeamInvitationDto(invitation.Id, email, role.ToString(), invitation.ExpiresAtUtc, link, emailed);
    }
}

public class RevokeInvitationCommandHandler : IRequestHandler<RevokeInvitationCommand, Unit>
{
    private readonly IAccountRepository _accounts;
    private readonly ITenantContext _tenant;
    private readonly IAuditService _audit;
    private readonly TimeProvider _clock;

    public RevokeInvitationCommandHandler(IAccountRepository accounts, ITenantContext tenant, IAuditService audit, TimeProvider clock)
    {
        _accounts = accounts;
        _tenant = tenant;
        _audit = audit;
        _clock = clock;
    }

    public async Task<Unit> Handle(RevokeInvitationCommand request, CancellationToken cancellationToken)
    {
        var link = await _accounts.GetLinkAsync(_tenant.OrganizationId, request.InvitationId, cancellationToken);
        if (link is null || link.Purpose != AccountLinkPurpose.Invitation)
        {
            throw new KeyNotFoundException("Invitation not found.");
        }

        link.RevokedAtUtc ??= _clock.GetUtcNow().UtcDateTime;
        await _accounts.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync("RevokeInvitation", "AccountLink", link.Id.ToString(), _tenant.UserId, link.Email, cancellationToken);
        return Unit.Value;
    }
}

public class ChangeTeammateRoleCommandHandler : IRequestHandler<ChangeTeammateRoleCommand, TeamMemberDto>
{
    private readonly IAccountRepository _accounts;
    private readonly IAccountSessions _sessions;
    private readonly ITenantContext _tenant;
    private readonly IAuditService _audit;

    public ChangeTeammateRoleCommandHandler(IAccountRepository accounts, IAccountSessions sessions, ITenantContext tenant, IAuditService audit)
    {
        _accounts = accounts;
        _sessions = sessions;
        _tenant = tenant;
        _audit = audit;
    }

    public async Task<TeamMemberDto> Handle(ChangeTeammateRoleCommand request, CancellationToken cancellationToken)
    {
        var user = await TeamSupport.OfficeMemberAsync(_accounts, _tenant, request.UserId, cancellationToken);
        var role = Enum.Parse<UserRole>(request.Role, ignoreCase: true);
        if (user.Role == role)
        {
            return TeamSupport.ToDto(user, _tenant);
        }

        if (user.Role == UserRole.Owner && user.IsActive && await _accounts.CountActiveOwnersAsync(_tenant.OrganizationId, cancellationToken) <= 1)
        {
            throw new BusinessRuleException("A company needs at least one owner. Make someone else an owner first.");
        }

        user.Role = role;
        await _accounts.SaveChangesAsync(cancellationToken);
        // What they may do is in their sign-in, so they sign in again with the new role.
        await _sessions.RevokeAllAsync(user.Id, cancellationToken);
        await _audit.LogAsync("ChangeTeammateRole", "UserAccount", user.Id.ToString(), _tenant.UserId, role.ToString(), cancellationToken);
        return TeamSupport.ToDto(user, _tenant);
    }
}

public class RemoveTeammateCommandHandler : IRequestHandler<RemoveTeammateCommand, Unit>
{
    private readonly IAccountRepository _accounts;
    private readonly IAccountSessions _sessions;
    private readonly ITenantContext _tenant;
    private readonly IAuditService _audit;

    public RemoveTeammateCommandHandler(IAccountRepository accounts, IAccountSessions sessions, ITenantContext tenant, IAuditService audit)
    {
        _accounts = accounts;
        _sessions = sessions;
        _tenant = tenant;
        _audit = audit;
    }

    public async Task<Unit> Handle(RemoveTeammateCommand request, CancellationToken cancellationToken)
    {
        var user = await TeamSupport.OfficeMemberAsync(_accounts, _tenant, request.UserId, cancellationToken);
        if (user.Id.ToString() == _tenant.UserId)
        {
            throw new BusinessRuleException("You can't remove yourself. Ask another owner.");
        }

        if (user.Role == UserRole.Owner && user.IsActive && await _accounts.CountActiveOwnersAsync(_tenant.OrganizationId, cancellationToken) <= 1)
        {
            throw new BusinessRuleException("A company needs at least one owner.");
        }

        user.IsActive = false;
        await _accounts.SaveChangesAsync(cancellationToken);
        await _sessions.RevokeAllAsync(user.Id, cancellationToken);
        await _audit.LogAsync("RemoveTeammate", "UserAccount", user.Id.ToString(), _tenant.UserId, user.Email, cancellationToken);
        return Unit.Value;
    }
}

public class CreatePasswordResetLinkCommandHandler : IRequestHandler<CreatePasswordResetLinkCommand, AccountLinkDto>
{
    private readonly IAccountRepository _accounts;
    private readonly ITenantContext _tenant;
    private readonly IEmailSender _email;
    private readonly IPublicLinks _links;
    private readonly IAuditService _audit;
    private readonly TimeProvider _clock;
    private readonly ILogger<CreatePasswordResetLinkCommandHandler> _logger;

    public CreatePasswordResetLinkCommandHandler(
        IAccountRepository accounts, ITenantContext tenant, IEmailSender email, IPublicLinks links, IAuditService audit,
        TimeProvider clock, ILogger<CreatePasswordResetLinkCommandHandler> logger)
    {
        _accounts = accounts;
        _tenant = tenant;
        _email = email;
        _links = links;
        _audit = audit;
        _clock = clock;
        _logger = logger;
    }

    public async Task<AccountLinkDto> Handle(CreatePasswordResetLinkCommand request, CancellationToken cancellationToken)
    {
        var user = await TeamSupport.OfficeMemberAsync(_accounts, _tenant, request.UserId, cancellationToken);
        if (!user.IsActive || user.Email is null)
        {
            throw new BusinessRuleException("This person can't sign in. Invite them again instead.");
        }

        var (link, token) = await TeamSupport.NewResetLinkAsync(_accounts, user, _tenant.UserId, _clock.GetUtcNow().UtcDateTime, cancellationToken);
        await _audit.LogAsync("CreatePasswordResetLink", "UserAccount", user.Id.ToString(), _tenant.UserId, null, cancellationToken);
        var url = _links.PasswordReset(token);
        var emailed = await TeamSupport.TrySendAsync(_email, _logger, user.Email, TeamText.PasswordReset(user.DisplayName, url), cancellationToken);
        return new AccountLinkDto(url, link.ExpiresAtUtc, emailed);
    }
}

public class GetInvitationQueryHandler : IRequestHandler<GetInvitationQuery, InvitationPreviewDto>
{
    private readonly IAccountRepository _accounts;
    private readonly TimeProvider _clock;

    public GetInvitationQueryHandler(IAccountRepository accounts, TimeProvider clock)
    {
        _accounts = accounts;
        _clock = clock;
    }

    public async Task<InvitationPreviewDto> Handle(GetInvitationQuery request, CancellationToken cancellationToken)
    {
        var link = await TeamSupport.OpenLinkAsync(_accounts, request.Token, AccountLinkPurpose.Invitation, _clock.GetUtcNow().UtcDateTime, cancellationToken);
        var company = await _accounts.OrganizationNameAsync(link.OrganizationId, cancellationToken) ?? string.Empty;
        return new InvitationPreviewDto(company, link.Email, link.Role.ToString());
    }
}

public class AcceptInvitationCommandHandler : IRequestHandler<AcceptInvitationCommand, AccountDto>
{
    private readonly IAccountRepository _accounts;
    private readonly IPasswordHasher _hasher;
    private readonly IAuditService _audit;
    private readonly TimeProvider _clock;

    public AcceptInvitationCommandHandler(IAccountRepository accounts, IPasswordHasher hasher, IAuditService audit, TimeProvider clock)
    {
        _accounts = accounts;
        _hasher = hasher;
        _audit = audit;
        _clock = clock;
    }

    public async Task<AccountDto> Handle(AcceptInvitationCommand request, CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var link = await TeamSupport.OpenLinkAsync(_accounts, request.Token, AccountLinkPurpose.Invitation, now, cancellationToken);
        if (await _accounts.GetByEmailAsync(link.Email, cancellationToken) is not null)
        {
            throw new BusinessRuleException("This email already has an OmniOps account. Sign in instead.");
        }

        var user = new UserAccount
        {
            Id = Guid.NewGuid(),
            OrganizationId = link.OrganizationId,
            Email = link.Email,
            DisplayName = request.Name.Trim(),
            PasswordHash = _hasher.Hash(request.Password),
            Role = link.Role,
            CreatedAtUtc = now,
            LastSignInAtUtc = now,
        };
        link.UsedAtUtc = now;
        await _accounts.AddAsync(user, cancellationToken);
        await _accounts.SaveChangesAsync(cancellationToken);
        await _audit.LogForAsync(link.OrganizationId, "AcceptInvitation", "UserAccount", user.Id.ToString(), user.Id.ToString(), link.Email, cancellationToken);
        return await AccountMapping.ToDtoAsync(user, _accounts, cancellationToken);
    }
}

public class RequestPasswordResetCommandHandler : IRequestHandler<RequestPasswordResetCommand, Unit>
{
    private readonly IAccountRepository _accounts;
    private readonly IEmailSender _email;
    private readonly IPublicLinks _links;
    private readonly TimeProvider _clock;
    private readonly ILogger<RequestPasswordResetCommandHandler> _logger;

    public RequestPasswordResetCommandHandler(
        IAccountRepository accounts, IEmailSender email, IPublicLinks links, TimeProvider clock, ILogger<RequestPasswordResetCommandHandler> logger)
    {
        _accounts = accounts;
        _email = email;
        _links = links;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Unit> Handle(RequestPasswordResetCommand request, CancellationToken cancellationToken)
    {
        // Without email the link could not reach anyone: the owner makes one from the team page instead.
        if (!_email.IsConfigured)
        {
            return Unit.Value;
        }

        var user = await _accounts.GetByEmailAsync(AccountRules.NormalizeEmail(request.Email), cancellationToken);
        if (user is not { IsActive: true, Email: { } email } || user.Role == UserRole.Driver)
        {
            return Unit.Value;
        }

        var (_, token) = await TeamSupport.NewResetLinkAsync(_accounts, user, null, _clock.GetUtcNow().UtcDateTime, cancellationToken);
        await TeamSupport.TrySendAsync(_email, _logger, email, TeamText.PasswordReset(user.DisplayName, _links.PasswordReset(token)), cancellationToken);
        return Unit.Value;
    }
}

public class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand, AccountDto>
{
    private readonly IAccountRepository _accounts;
    private readonly IAccountSessions _sessions;
    private readonly IPasswordHasher _hasher;
    private readonly IAuditService _audit;
    private readonly TimeProvider _clock;

    public ResetPasswordCommandHandler(
        IAccountRepository accounts, IAccountSessions sessions, IPasswordHasher hasher, IAuditService audit, TimeProvider clock)
    {
        _accounts = accounts;
        _sessions = sessions;
        _hasher = hasher;
        _audit = audit;
        _clock = clock;
    }

    public async Task<AccountDto> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var link = await TeamSupport.OpenLinkAsync(_accounts, request.Token, AccountLinkPurpose.PasswordReset, now, cancellationToken);
        var user = link.UserId is { } id ? await _accounts.GetByIdAsync(id, cancellationToken) : null;
        if (user is not { IsActive: true } || user.OrganizationId != link.OrganizationId)
        {
            throw new KeyNotFoundException("This link has expired or was already used. Ask for a new one.");
        }

        link.UsedAtUtc = now;
        user.PasswordHash = _hasher.Hash(request.Password);
        user.FailedSignIns = 0;
        user.LockedUntilUtc = null;
        user.LastSignInAtUtc = now;
        await _accounts.SaveChangesAsync(cancellationToken);
        await _sessions.RevokeAllAsync(user.Id, cancellationToken);
        await _audit.LogForAsync(user.OrganizationId, "ResetPassword", "UserAccount", user.Id.ToString(), user.Id.ToString(), null, cancellationToken);
        return await AccountMapping.ToDtoAsync(user, _accounts, cancellationToken);
    }
}

public class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand, AccountDto>
{
    private readonly IAccountRepository _accounts;
    private readonly IAccountSessions _sessions;
    private readonly IPasswordHasher _hasher;
    private readonly ITenantContext _tenant;
    private readonly IAuditService _audit;

    public ChangePasswordCommandHandler(
        IAccountRepository accounts, IAccountSessions sessions, IPasswordHasher hasher, ITenantContext tenant, IAuditService audit)
    {
        _accounts = accounts;
        _sessions = sessions;
        _hasher = hasher;
        _tenant = tenant;
        _audit = audit;
    }

    public async Task<AccountDto> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await SelfAsync(_accounts, _tenant, cancellationToken);
        // 409 rather than 401: the person is signed in, only the typed password is wrong.
        if (user.PasswordHash is null || !_hasher.Verify(request.CurrentPassword, user.PasswordHash, out _))
        {
            throw new BusinessRuleException("Your current password is not right.");
        }

        user.PasswordHash = _hasher.Hash(request.NewPassword);
        await _accounts.SaveChangesAsync(cancellationToken);
        await _sessions.RevokeAllAsync(user.Id, cancellationToken);
        await _audit.LogAsync("ChangePassword", "UserAccount", user.Id.ToString(), _tenant.UserId, null, cancellationToken);
        return await AccountMapping.ToDtoAsync(user, _accounts, cancellationToken);
    }

    internal static async Task<UserAccount> SelfAsync(IAccountRepository accounts, ITenantContext tenant, CancellationToken ct) =>
        Guid.TryParse(tenant.UserId, out var id) && await accounts.GetByIdAsync(id, ct) is { IsActive: true } user
            && user.OrganizationId == tenant.OrganizationId
            ? user
            : throw new AuthenticationFailedException("Sign in to continue.");
}

public class UpdateProfileCommandHandler : IRequestHandler<UpdateProfileCommand, AccountDto>
{
    private readonly IAccountRepository _accounts;
    private readonly ITenantContext _tenant;

    public UpdateProfileCommandHandler(IAccountRepository accounts, ITenantContext tenant)
    {
        _accounts = accounts;
        _tenant = tenant;
    }

    public async Task<AccountDto> Handle(UpdateProfileCommand request, CancellationToken cancellationToken)
    {
        var user = await ChangePasswordCommandHandler.SelfAsync(_accounts, _tenant, cancellationToken);
        user.DisplayName = request.DisplayName.Trim();
        await _accounts.SaveChangesAsync(cancellationToken);
        return await AccountMapping.ToDtoAsync(user, _accounts, cancellationToken);
    }
}

public class SignOutEverywhereCommandHandler : IRequestHandler<SignOutEverywhereCommand, Unit>
{
    private readonly IAccountRepository _accounts;
    private readonly IAccountSessions _sessions;
    private readonly ITenantContext _tenant;

    public SignOutEverywhereCommandHandler(IAccountRepository accounts, IAccountSessions sessions, ITenantContext tenant)
    {
        _accounts = accounts;
        _sessions = sessions;
        _tenant = tenant;
    }

    public async Task<Unit> Handle(SignOutEverywhereCommand request, CancellationToken cancellationToken)
    {
        var user = await ChangePasswordCommandHandler.SelfAsync(_accounts, _tenant, cancellationToken);
        await _sessions.RevokeAllAsync(user.Id, cancellationToken);
        return Unit.Value;
    }
}

public class UpdateCompanyCommandHandler : IRequestHandler<UpdateCompanyCommand, CompanyDto>
{
    private readonly IAccountRepository _accounts;
    private readonly ITenantContext _tenant;
    private readonly IAuditService _audit;

    public UpdateCompanyCommandHandler(IAccountRepository accounts, ITenantContext tenant, IAuditService audit)
    {
        _accounts = accounts;
        _tenant = tenant;
        _audit = audit;
    }

    public async Task<CompanyDto> Handle(UpdateCompanyCommand request, CancellationToken cancellationToken)
    {
        var organization = await _accounts.GetOrganizationAsync(_tenant.OrganizationId, cancellationToken)
            ?? throw new KeyNotFoundException("Company not found.");
        organization.Name = request.Name.Trim();
        await _accounts.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync("UpdateCompany", "Organization", organization.Id.ToString(), _tenant.UserId, organization.Name, cancellationToken);
        return new CompanyDto(organization.Id, organization.Name);
    }
}
