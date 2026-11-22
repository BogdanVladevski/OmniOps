using MediatR;
using OmniOps.Application.Abstractions;
using OmniOps.Application.Dtos;
using OmniOps.Core.Authorization;

namespace OmniOps.Application.Commands;

// Team: owners invite office staff and manage who can sign in. Drivers join with pairing codes instead.

public record GetTeamQuery : IRequest<TeamOverviewDto>, IAuthorizedRequest
{
    public string RequiredPolicy => AuthorizationPolicies.FleetAdmin;
}

/// <summary>Role is "Owner" or "Dispatcher".</summary>
public record InviteTeammateCommand(string Email, string Role) : IRequest<TeamInvitationDto>, IAuthorizedRequest
{
    public string RequiredPolicy => AuthorizationPolicies.FleetAdmin;
}

public record RevokeInvitationCommand(Guid InvitationId) : IRequest<Unit>, IAuthorizedRequest
{
    public string RequiredPolicy => AuthorizationPolicies.FleetAdmin;
}

public record ChangeTeammateRoleCommand(Guid UserId, string Role) : IRequest<TeamMemberDto>, IAuthorizedRequest
{
    public string RequiredPolicy => AuthorizationPolicies.FleetAdmin;
}

/// <summary>Switches the account off and ends its sign-ins. Its history (jobs, audit log) stays.</summary>
public record RemoveTeammateCommand(Guid UserId) : IRequest<Unit>, IAuthorizedRequest
{
    public string RequiredPolicy => AuthorizationPolicies.FleetAdmin;
}

/// <summary>For teammates who forgot their password when no email can be sent: the owner hands them the link.</summary>
public record CreatePasswordResetLinkCommand(Guid UserId) : IRequest<AccountLinkDto>, IAuthorizedRequest
{
    public string RequiredPolicy => AuthorizationPolicies.FleetAdmin;
}

/// <summary>Anonymous: details shown on the "join company" screen. Not found when used, revoked or expired.</summary>
public record GetInvitationQuery(string Token) : IRequest<InvitationPreviewDto>;

/// <summary>Anonymous: creates the invited person's account and signs them in.</summary>
public record AcceptInvitationCommand(string Token, string Name, string Password) : IRequest<AccountDto>;

/// <summary>Anonymous. Always succeeds, so nobody can learn which emails have accounts.</summary>
public record RequestPasswordResetCommand(string Email) : IRequest<Unit>;

/// <summary>Anonymous: sets a new password from a reset link, ends every other sign-in and signs in.</summary>
public record ResetPasswordCommand(string Token, string Password) : IRequest<AccountDto>;

/// <summary>Signed-in office user. Ends their other sign-ins.</summary>
public record ChangePasswordCommand(string CurrentPassword, string NewPassword) : IRequest<AccountDto>, IAuthorizedRequest
{
    public string RequiredPolicy => AuthorizationPolicies.VehicleRead;
}

public record UpdateProfileCommand(string DisplayName) : IRequest<AccountDto>, IAuthorizedRequest
{
    public string RequiredPolicy => AuthorizationPolicies.VehicleRead;
}

/// <summary>Signs the caller out on every device, this one included.</summary>
public record SignOutEverywhereCommand : IRequest<Unit>, IAuthorizedRequest
{
    public string RequiredPolicy => AuthorizationPolicies.JobWork;
}

public record UpdateCompanyCommand(string Name) : IRequest<CompanyDto>, IAuthorizedRequest
{
    public string RequiredPolicy => AuthorizationPolicies.FleetAdmin;
}
