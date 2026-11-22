using MediatR;
using Microsoft.Extensions.Options;
using OmniOps.Api.Authentication;
using OmniOps.Application.Commands;
using OmniOps.Application.Dtos;
using OmniOps.Core.Entities;
using OmniOps.Core.Enums;
using OmniOps.Core.Exceptions;
using OmniOps.Core.Interfaces;
using OmniOps.Infrastructure.Configuration;

namespace OmniOps.Api.Endpoints;

/// <summary>Company sign-up, sign-in, driver pairing and sessions.</summary>
public static class AccountEndpoints
{
    public static void MapAccountEndpoints(this WebApplication app)
    {
        var requireAuth = app.Services.GetRequiredService<IOptions<JwtOptions>>().Value.RequireAuthentication;
        var account = app.MapGroup("/api/v1/account").WithTags("Account").WithOpenApi();

        account.MapPost("/sign-up", async (SignUpBody body, IMediator m, AccountTokenIssuer issuer) =>
                Results.Ok(await issuer.IssueAsync(await m.Send(new SignUpCommand(
                    body.CompanyName ?? string.Empty, body.Name ?? string.Empty, body.Email ?? string.Empty, body.Password ?? string.Empty)))))
            .RequireRateLimiting(ServiceRegistration.AccountPolicy).AllowAnonymous();

        account.MapPost("/sign-in", async (SignInBody body, IMediator m, AccountTokenIssuer issuer) =>
                Results.Ok(await issuer.IssueAsync(await m.Send(new SignInCommand(body.Email ?? string.Empty, body.Password ?? string.Empty)))))
            .RequireRateLimiting(ServiceRegistration.AccountPolicy).AllowAnonymous();

        account.MapPost("/pair", async (PairBody body, IMediator m, AccountTokenIssuer issuer) =>
                Results.Ok(await issuer.IssueAsync(await m.Send(new PairDriverCommand(body.Code ?? string.Empty)))))
            .RequireRateLimiting(ServiceRegistration.AccountPolicy).AllowAnonymous();

        // Refresh tokens are single use; the new session reflects the account's current role.
        account.MapPost("/refresh", async (RefreshBody body, IRefreshTokenService refresh, IMediator m, AccountTokenIssuer issuer) =>
            {
                var rotated = await refresh.RotateAsync(body.RefreshToken ?? string.Empty);
                if (rotated is not { } r || !Guid.TryParse(r.UserId, out var userId)
                    || await m.Send(new GetAccountQuery(userId)) is not { } current)
                {
                    return Results.Unauthorized();
                }

                await refresh.RevokeAsync(r.Token); // the issuer creates its own refresh token
                return Results.Ok(await issuer.IssueAsync(current));
            })
            .RequireRateLimiting(ServiceRegistration.AccountPolicy).AllowAnonymous();

        account.MapPost("/sign-out", async (RefreshBody body, IRefreshTokenService refresh) =>
            {
                if (!string.IsNullOrWhiteSpace(body.RefreshToken))
                {
                    await refresh.RevokeAsync(body.RefreshToken);
                }

                return Results.NoContent();
            })
            .AllowAnonymous();

        var me = account.MapGet("/me", async (ITenantContext tenant, IMediator m) =>
            Guid.TryParse(tenant.UserId, out var id) && await m.Send(new GetAccountQuery(id)) is { } current
                ? Results.Ok(current)
                : Results.Unauthorized());

        var pairing = account.MapPost("/driver-codes", async (PairingBody body, IMediator m) =>
            Results.Ok(await m.Send(new CreateDriverPairingCodeCommand(body.DriverId))));

        if (requireAuth)
        {
            me.RequireAuthorization(AuthorizationPolicies.JobWork);
            pairing.RequireAuthorization(AuthorizationPolicies.VehicleRead);
        }

        MapTeam(account, requireAuth);
        MapPasswords(account, requireAuth);

        // What the sign-in screen may offer. The demo is for local development only.
        account.MapGet("/options", (IWebHostEnvironment env, IEmailSender email) =>
            Results.Ok(new { demo = env.IsDevelopment(), passwordResetByEmail = email.IsConfigured })).AllowAnonymous();

        account.MapPost("/demo", async (IWebHostEnvironment env, IAccountRepository accounts, IMediator m, AccountTokenIssuer issuer) =>
            {
                if (!env.IsDevelopment())
                {
                    return Results.NotFound();
                }

                var demo = await DemoOwnerAsync(accounts);
                var session = await m.Send(new GetAccountQuery(demo.Id))
                    ?? throw new AuthenticationFailedException("The demo account is not available.");
                // The demo may also run the telemetry simulator and operator tools.
                return Results.Ok(await issuer.IssueAsync(session,
                    [AuthorizationPolicies.VehicleSimulateScope, AuthorizationPolicies.PlatformAdminScope]));
            })
            .AllowAnonymous();
    }

    private static RouteHandlerBuilder Secure(this RouteHandlerBuilder route, bool requireAuth, string policy) =>
        requireAuth ? route.RequireAuthorization(policy) : route;

    private static RouteHandlerBuilder Anonymous(this RouteHandlerBuilder route) =>
        route.RequireRateLimiting(ServiceRegistration.AccountPolicy).AllowAnonymous();

    /// <summary>Owners manage who in the office can sign in; invited people accept from a link.</summary>
    private static void MapTeam(RouteGroupBuilder account, bool requireAuth)
    {
        const string admin = AuthorizationPolicies.FleetAdmin;

        account.MapGet("/team", async (IMediator m) => Results.Ok(await m.Send(new GetTeamQuery()))).Secure(requireAuth, admin);

        account.MapPost("/team/invitations", async (InviteBody body, IMediator m) =>
                Results.Ok(await m.Send(new InviteTeammateCommand(body.Email ?? string.Empty, body.Role ?? string.Empty))))
            .Secure(requireAuth, admin);

        account.MapDelete("/team/invitations/{id:guid}", async (Guid id, IMediator m) =>
            {
                await m.Send(new RevokeInvitationCommand(id));
                return Results.NoContent();
            })
            .Secure(requireAuth, admin);

        account.MapPut("/team/{userId:guid}/role", async (Guid userId, RoleBody body, IMediator m) =>
                Results.Ok(await m.Send(new ChangeTeammateRoleCommand(userId, body.Role ?? string.Empty))))
            .Secure(requireAuth, admin);

        account.MapDelete("/team/{userId:guid}", async (Guid userId, IMediator m) =>
            {
                await m.Send(new RemoveTeammateCommand(userId));
                return Results.NoContent();
            })
            .Secure(requireAuth, admin);

        account.MapPost("/team/{userId:guid}/reset-link", async (Guid userId, IMediator m) =>
                Results.Ok(await m.Send(new CreatePasswordResetLinkCommand(userId))))
            .Secure(requireAuth, admin);

        account.MapPut("/company", async (CompanyBody body, IMediator m) =>
                Results.Ok(await m.Send(new UpdateCompanyCommand(body.Name ?? string.Empty))))
            .Secure(requireAuth, admin);

        account.MapGet("/invitations/{token}", async (string token, IMediator m) =>
                Results.Ok(await m.Send(new GetInvitationQuery(token))))
            .Anonymous();

        account.MapPost("/invitations/{token}/accept", async (string token, AcceptBody body, IMediator m, AccountTokenIssuer issuer) =>
                Results.Ok(await issuer.IssueAsync(await m.Send(new AcceptInvitationCommand(
                    token, body.Name ?? string.Empty, body.Password ?? string.Empty)))))
            .Anonymous();
    }

    private static void MapPasswords(RouteGroupBuilder account, bool requireAuth)
    {
        // Same answer whether or not the email has an account.
        account.MapPost("/forgot-password", async (ForgotBody body, IMediator m) =>
            {
                await m.Send(new RequestPasswordResetCommand(body.Email ?? string.Empty));
                return Results.Accepted();
            })
            .Anonymous();

        account.MapPost("/reset-password", async (ResetBody body, IMediator m, AccountTokenIssuer issuer) =>
                Results.Ok(await issuer.IssueAsync(await m.Send(new ResetPasswordCommand(
                    body.Token ?? string.Empty, body.Password ?? string.Empty)))))
            .Anonymous();

        // Other devices are signed out; this one gets a fresh session.
        account.MapPost("/password", async (ChangePasswordBody body, IMediator m, AccountTokenIssuer issuer) =>
                Results.Ok(await issuer.IssueAsync(await m.Send(new ChangePasswordCommand(
                    body.CurrentPassword ?? string.Empty, body.NewPassword ?? string.Empty)))))
            .Secure(requireAuth, AuthorizationPolicies.VehicleRead);

        account.MapPut("/me", async (ProfileBody body, IMediator m) =>
                Results.Ok(await m.Send(new UpdateProfileCommand(body.DisplayName ?? string.Empty))))
            .Secure(requireAuth, AuthorizationPolicies.VehicleRead);

        account.MapPost("/sign-out-everywhere", async (IMediator m) =>
            {
                await m.Send(new SignOutEverywhereCommand());
                return Results.NoContent();
            })
            .Secure(requireAuth, AuthorizationPolicies.JobWork);
    }

    /// <summary>Owner of the seeded demo organization (development only). Has no password: it cannot be signed into otherwise.</summary>
    private static async Task<UserAccount> DemoOwnerAsync(IAccountRepository accounts)
    {
        const string email = "demo@omniops.local";
        var existing = await accounts.GetByEmailAsync(email);
        if (existing is not null)
        {
            return existing;
        }

        var owner = new UserAccount
        {
            Id = Guid.NewGuid(),
            OrganizationId = TenantSeed.DefaultOrganizationId,
            Email = email,
            DisplayName = "Demo owner",
            Role = UserRole.Owner,
            CreatedAtUtc = DateTime.UtcNow,
        };
        await accounts.AddAsync(owner);
        await accounts.SaveChangesAsync();
        return owner;
    }

    private record SignUpBody(string? CompanyName, string? Name, string? Email, string? Password);

    private record SignInBody(string? Email, string? Password);

    private record PairBody(string? Code);

    private record RefreshBody(string? RefreshToken);

    private record PairingBody(Guid DriverId);

    private record InviteBody(string? Email, string? Role);

    private record RoleBody(string? Role);

    private record CompanyBody(string? Name);

    private record AcceptBody(string? Name, string? Password);

    private record ForgotBody(string? Email);

    private record ResetBody(string? Token, string? Password);

    private record ChangePasswordBody(string? CurrentPassword, string? NewPassword);

    private record ProfileBody(string? DisplayName);
}
