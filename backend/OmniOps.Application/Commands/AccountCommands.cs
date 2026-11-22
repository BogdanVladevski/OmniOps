using MediatR;
using OmniOps.Application.Abstractions;
using OmniOps.Application.Dtos;
using OmniOps.Core.Authorization;

namespace OmniOps.Application.Commands;

/// <summary>Anonymous: creates a company account (organization, first fleet, owner).</summary>
public record SignUpCommand(string CompanyName, string Name, string Email, string Password) : IRequest<AccountDto>;

/// <summary>Anonymous: office sign-in with email and password.</summary>
public record SignInCommand(string Email, string Password) : IRequest<AccountDto>;

/// <summary>Office: a one-time code a driver types to sign in their phone (valid 24 hours).</summary>
public record CreateDriverPairingCodeCommand(Guid DriverId) : IRequest<DriverPairingCodeDto>, IAuthorizedRequest
{
    public string RequiredPolicy => AuthorizationPolicies.VehicleRead;
}

/// <summary>Anonymous: a driver's phone exchanges a pairing code for a driver sign-in.</summary>
public record PairDriverCommand(string Code) : IRequest<AccountDto>;

/// <summary>Reloads an account by id (token refresh). Null if it no longer exists or was switched off.</summary>
public record GetAccountQuery(Guid UserId) : IRequest<AccountDto?>;
