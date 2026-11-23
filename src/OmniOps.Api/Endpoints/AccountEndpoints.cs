using Mediator;
using Microsoft.AspNetCore.Http.HttpResults;
using OmniOps.Api.Authentication;
using OmniOps.Api.Infrastructure;
using OmniOps.Application.Common.Interfaces;
using OmniOps.Application.Common.Security;
using OmniOps.Application.Features.Accounts;
using OmniOps.Application.Features.Demo;

namespace OmniOps.Api.Endpoints;

/// <summary>Company sign-up, sign-in, driver pairing, sessions, team and passwords.</summary>
internal static class AccountEndpoints
{
    public static void MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        var account = app.MapGroup("/api/v1/account").WithTags("Account");

        // Anonymous, rate limited: each of these proves who you are some other way (password, code, link).
        var open = account.MapGroup("").AllowAnonymous().RequireRateLimiting(RateLimits.Accounts);
        open.MapPost("/sign-up", async (SignUpCommand command, IMediator m, TokenService tokens, CancellationToken ct) =>
            TypedResults.Ok(await tokens.IssueAsync(await m.Send(command, ct), ct)));
        open.MapPost("/sign-in", async (SignInCommand command, IMediator m, TokenService tokens, CancellationToken ct) =>
            TypedResults.Ok(await tokens.IssueAsync(await m.Send(command, ct), ct)));
        open.MapPost("/pair", async (PairDriverCommand command, IMediator m, TokenService tokens, CancellationToken ct) =>
            TypedResults.Ok(await tokens.IssueAsync(await m.Send(command, ct), ct)));
        open.MapPost("/refresh", Refresh);
        open.MapPost("/sign-out", async (RefreshBody body, TokenService tokens, CancellationToken ct) =>
        {
            if (!string.IsNullOrWhiteSpace(body.RefreshToken))
            {
                await tokens.RevokeRefreshTokenAsync(body.RefreshToken, ct);
            }

            return TypedResults.NoContent();
        });
        open.MapGet("/invitations/{token}", async (string token, IMediator m, CancellationToken ct) =>
            TypedResults.Ok(await m.Send(new GetInvitationQuery(token), ct)));
        open.MapPost("/invitations/{token}/accept", async (string token, AcceptBody body, IMediator m, TokenService tokens, CancellationToken ct) =>
            TypedResults.Ok(await tokens.IssueAsync(await m.Send(new AcceptInvitationCommand(token, body.Name ?? "", body.Password ?? ""), ct), ct)));
        // Same answer whether or not the email has an account.
        open.MapPost("/forgot-password", async (RequestPasswordResetCommand command, IMediator m, CancellationToken ct) =>
        {
            await m.Send(command, ct);
            return TypedResults.Accepted((string?)null);
        });
        open.MapPost("/reset-password", async (ResetPasswordCommand command, IMediator m, TokenService tokens, CancellationToken ct) =>
            TypedResults.Ok(await tokens.IssueAsync(await m.Send(command, ct), ct)));

        // What the sign-in screen may offer. The demo company is for development servers only.
        account.MapGet("/options", (IWebHostEnvironment env, IEmailSender email) =>
            TypedResults.Ok(new SignInOptionsDto(env.IsDevelopment(), email.IsConfigured))).AllowAnonymous();
        account.MapPost("/demo", async Task<Results<Ok<SessionDto>, NotFound>> (IWebHostEnvironment env, IMediator m, TokenService tokens, CancellationToken ct) =>
            {
                if (!env.IsDevelopment())
                {
                    return TypedResults.NotFound();
                }

                var ownerId = await m.Send(new EnsureDemoCompanyCommand(), ct);
                var session = await m.Send(new GetAccountQuery(ownerId), ct) ?? throw new InvalidOperationException("Demo owner missing.");
                return TypedResults.Ok(await tokens.IssueAsync(session, ct));
            })
            .AllowAnonymous()
            .WithSummary("Signs in to the demo company (development servers only; 404 elsewhere).");

        // Signed in.
        account.MapGet("/me", async Task<Results<Ok<AccountDto>, UnauthorizedHttpResult>> (ITenantContext tenant, IMediator m, CancellationToken ct) =>
                tenant.UserId is { } id && await m.Send(new GetAccountQuery(id), ct) is { } me ? TypedResults.Ok(me) : TypedResults.Unauthorized())
            .RequireAuthorization(Policies.JobWork);
        account.MapPut("/me", async (UpdateProfileCommand command, IMediator m, CancellationToken ct) => TypedResults.Ok(await m.Send(command, ct)))
            .RequireAuthorization(Policies.Office);
        account.MapPost("/password", async (ChangePasswordCommand command, IMediator m, TokenService tokens, CancellationToken ct) =>
                TypedResults.Ok(await tokens.IssueAsync(await m.Send(command, ct), ct)))
            .RequireAuthorization(Policies.Office);
        account.MapPost("/sign-out-everywhere", async (IMediator m, CancellationToken ct) =>
            {
                await m.Send(new SignOutEverywhereCommand(), ct);
                return TypedResults.NoContent();
            })
            .RequireAuthorization(Policies.JobWork);
        account.MapPost("/driver-codes", async (CreateDriverPairingCodeCommand command, IMediator m, CancellationToken ct) =>
                TypedResults.Ok(await m.Send(command, ct)))
            .RequireAuthorization(Policies.Office);

        // Owners: company and team.
        var owner = account.MapGroup("").RequireAuthorization(Policies.Owner);
        owner.MapPut("/company", async (UpdateCompanyCommand command, IMediator m, CancellationToken ct) => TypedResults.Ok(await m.Send(command, ct)));
        owner.MapGet("/team", async (IMediator m, CancellationToken ct) => TypedResults.Ok(await m.Send(new GetTeamQuery(), ct)));
        owner.MapPost("/team/invitations", async (InviteTeammateCommand command, IMediator m, CancellationToken ct) =>
            TypedResults.Ok(await m.Send(command, ct)));
        owner.MapDelete("/team/invitations/{id:guid}", async (Guid id, IMediator m, CancellationToken ct) =>
        {
            await m.Send(new RevokeInvitationCommand(id), ct);
            return TypedResults.NoContent();
        });
        owner.MapPut("/team/{userId:guid}/role", async (Guid userId, RoleBody body, IMediator m, CancellationToken ct) =>
            TypedResults.Ok(await m.Send(new ChangeTeammateRoleCommand(userId, body.Role ?? ""), ct)));
        owner.MapDelete("/team/{userId:guid}", async (Guid userId, IMediator m, CancellationToken ct) =>
        {
            await m.Send(new RemoveTeammateCommand(userId), ct);
            return TypedResults.NoContent();
        });
        owner.MapPost("/team/{userId:guid}/reset-link", async (Guid userId, IMediator m, CancellationToken ct) =>
            TypedResults.Ok(await m.Send(new CreatePasswordResetLinkCommand(userId), ct)));
    }

    /// <summary>Refresh tokens are single use; the new session reflects the account's current role.</summary>
    private static async Task<Results<Ok<SessionDto>, UnauthorizedHttpResult>> Refresh(RefreshBody body, TokenService tokens, IMediator m, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.RefreshToken)
            || await tokens.RedeemRefreshTokenAsync(body.RefreshToken, ct) is not { } userId
            || await m.Send(new GetAccountQuery(userId), ct) is not { } account)
        {
            return TypedResults.Unauthorized();
        }

        return TypedResults.Ok(await tokens.IssueAsync(account, ct));
    }

    /// <summary>What the sign-in screen may offer.</summary>
    internal sealed record SignInOptionsDto(bool Demo, bool PasswordResetByEmail);

    internal sealed record RefreshBody(string? RefreshToken);

    internal sealed record AcceptBody(string? Name, string? Password);

    internal sealed record RoleBody(string? Role);
}
