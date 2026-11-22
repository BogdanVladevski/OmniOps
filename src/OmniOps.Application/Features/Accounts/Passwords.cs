using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OmniOps.Application.Common.Interfaces;
using OmniOps.Application.Common.Security;
using OmniOps.Domain.Accounts;
using OmniOps.Domain.Common;

namespace OmniOps.Application.Features.Accounts;

/// <summary>Anonymous. Always succeeds, so nobody can learn which emails have accounts.</summary>
public sealed record RequestPasswordResetCommand(string Email) : ICommand;

public sealed class RequestPasswordResetValidator : AbstractValidator<RequestPasswordResetCommand>
{
    public RequestPasswordResetValidator() => RuleFor(x => x.Email).NotEmpty().WithMessage("Enter your email.").MaximumLength(200);
}

public sealed class RequestPasswordResetHandler(
    IAppDbContext db, IEmailSender email, IPublicLinks links, TimeProvider clock, ILogger<RequestPasswordResetHandler> logger)
    : ICommandHandler<RequestPasswordResetCommand>
{
    public async ValueTask<Unit> Handle(RequestPasswordResetCommand command, CancellationToken cancellationToken)
    {
        // Without email the link could not reach anyone: an owner makes one from the team page instead.
        if (!email.IsConfigured)
        {
            return Unit.Value;
        }

        var address = AccountRules.NormalizeEmail(command.Email);
        var user = await db.Users.AcrossCompanies().FirstOrDefaultAsync(u => u.Email == address, cancellationToken);
        if (user is not { IsActive: true, Email: { } to } || user.Role == UserRole.Driver)
        {
            return Unit.Value;
        }

        var token = AccountRules.NewLinkToken();
        db.AccountLinks.Add(AccountLinks.NewPasswordReset(user, token, null, clock.GetUtcNow().UtcDateTime));
        await db.SaveChangesAsync(cancellationToken);
        await email.TrySendAsync(logger, to, AccountEmails.PasswordReset(user.DisplayName, links.PasswordReset(token)), cancellationToken);
        return Unit.Value;
    }
}

/// <summary>For teammates who forgot their password when no email can be sent: the owner hands them the link.</summary>
public sealed record CreatePasswordResetLinkCommand(Guid UserId) : ICommand<AccountLinkDto>, IRequirePolicy
{
    public string Policy => Policies.Owner;
}

public sealed class CreatePasswordResetLinkHandler(
    IAppDbContext db, ITenantContext tenant, IEmailSender email, IPublicLinks links, IAuditLog audit, TimeProvider clock,
    ILogger<CreatePasswordResetLinkHandler> logger) : ICommandHandler<CreatePasswordResetLinkCommand, AccountLinkDto>
{
    public async ValueTask<AccountLinkDto> Handle(CreatePasswordResetLinkCommand command, CancellationToken cancellationToken)
    {
        var user = await db.OfficeMemberAsync(command.UserId, cancellationToken);
        if (!user.IsActive || user.Email is null)
        {
            throw new BusinessRuleException("This person can't sign in. Invite them again instead.");
        }

        var token = AccountRules.NewLinkToken();
        var link = AccountLinks.NewPasswordReset(user, token, tenant.UserId, clock.GetUtcNow().UtcDateTime);
        db.AccountLinks.Add(link);
        audit.Record("CreatePasswordResetLink", "UserAccount", user.Id.ToString());
        await db.SaveChangesAsync(cancellationToken);

        var url = links.PasswordReset(token);
        var emailed = await email.TrySendAsync(logger, user.Email, AccountEmails.PasswordReset(user.DisplayName, url), cancellationToken);
        return new AccountLinkDto(url, link.ExpiresAtUtc, emailed);
    }
}

/// <summary>Anonymous: sets a new password from a reset link, ends every other sign-in and signs in.</summary>
public sealed record ResetPasswordCommand(string Token, string Password) : ICommand<AccountDto>;

public sealed class ResetPasswordValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordValidator()
    {
        RuleFor(x => x.Token).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Password).NewPassword();
    }
}

public sealed class ResetPasswordHandler(IAppDbContext db, IAccountSessions sessions, IPasswordHasher hasher, IAuditLog audit, TimeProvider clock)
    : ICommandHandler<ResetPasswordCommand, AccountDto>
{
    public async ValueTask<AccountDto> Handle(ResetPasswordCommand command, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var link = await db.OpenLinkAsync(command.Token, AccountLinkPurpose.PasswordReset, now, cancellationToken);
        var user = await db.Users.AcrossCompanies().FirstOrDefaultAsync(u => u.Id == link.UserId, cancellationToken);
        if (user is not { IsActive: true } || user.OrganizationId != link.OrganizationId)
        {
            throw new KeyNotFoundException("This link has expired or was already used. Ask for a new one.");
        }

        link.UsedAtUtc = now;
        user.PasswordHash = hasher.Hash(command.Password);
        user.FailedSignIns = 0;
        user.LockedUntilUtc = null;
        user.LastSignInAtUtc = now;
        audit.Record(user.OrganizationId, user.Id, "ResetPassword", "UserAccount", user.Id.ToString());
        await db.SaveChangesAsync(cancellationToken);
        await sessions.RevokeAllAsync(user.Id, cancellationToken);
        return await user.ToDtoAsync(db, cancellationToken);
    }
}

/// <summary>Signed-in office user. Ends their other sign-ins.</summary>
public sealed record ChangePasswordCommand(string CurrentPassword, string NewPassword) : ICommand<AccountDto>, IRequirePolicy
{
    public string Policy => Policies.Office;
}

public sealed class ChangePasswordValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty().WithMessage("Enter your current password.").MaximumLength(200);
        RuleFor(x => x.NewPassword).NewPassword();
    }
}

public sealed class ChangePasswordHandler(IAppDbContext db, IAccountSessions sessions, IPasswordHasher hasher, ITenantContext tenant, IAuditLog audit)
    : ICommandHandler<ChangePasswordCommand, AccountDto>
{
    public async ValueTask<AccountDto> Handle(ChangePasswordCommand command, CancellationToken cancellationToken)
    {
        var user = await db.SelfAsync(tenant, cancellationToken);
        // 409 rather than 401: the person is signed in, only the typed password is wrong.
        if (user.PasswordHash is null || !hasher.Verify(command.CurrentPassword, user.PasswordHash, out _))
        {
            throw new BusinessRuleException("Your current password is not right.");
        }

        user.PasswordHash = hasher.Hash(command.NewPassword);
        audit.Record("ChangePassword", "UserAccount", user.Id.ToString());
        await db.SaveChangesAsync(cancellationToken);
        await sessions.RevokeAllAsync(user.Id, cancellationToken);
        return await user.ToDtoAsync(db, cancellationToken);
    }
}

internal static class SelfQueries
{
    public static async Task<UserAccount> SelfAsync(this IAppDbContext db, ITenantContext tenant, CancellationToken ct) =>
        await db.Users.FirstOrDefaultAsync(u => u.Id == tenant.UserId && u.IsActive, ct)
        ?? throw new AuthenticationFailedException("Sign in to continue.");
}
