using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;
using OmniOps.Application.Common.Interfaces;
using OmniOps.Application.Common.Security;
using OmniOps.Domain.Common;

namespace OmniOps.Application.Features.Accounts;

/// <summary>Anonymous: office sign-in with email and password. Five wrong passwords lock the account for 15 minutes.</summary>
[Anonymous]
public sealed record SignInCommand(string Email, string Password) : ICommand<AccountDto>;

public sealed class SignInValidator : AbstractValidator<SignInCommand>
{
    public SignInValidator()
    {
        RuleFor(x => x.Email).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(200);
    }
}

public sealed class SignInHandler(IAppDbContext db, IPasswordHasher hasher, TimeProvider clock) : ICommandHandler<SignInCommand, AccountDto>
{
    // Checked when the email is unknown, so a wrong email takes as long as a wrong password.
    private static string? _decoyHash;

    public async ValueTask<AccountDto> Handle(SignInCommand command, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var email = AccountRules.NormalizeEmail(command.Email);
        var user = await db.Users.AcrossCompanies().FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
        if (user?.PasswordHash is null || !user.IsActive)
        {
            hasher.Verify(command.Password, _decoyHash ??= hasher.Hash("not-a-real-password"), out _);
            throw new AuthenticationFailedException();
        }

        if (user.LockedUntilUtc > now)
        {
            throw new AuthenticationFailedException("Too many attempts. Wait 15 minutes and try again.");
        }

        if (!hasher.Verify(command.Password, user.PasswordHash, out var needsRehash))
        {
            user.FailedSignIns++;
            if (user.FailedSignIns >= AccountRules.MaxFailedSignIns)
            {
                user.LockedUntilUtc = now + AccountRules.Lockout;
                user.FailedSignIns = 0;
            }

            await db.SaveChangesAsync(cancellationToken);
            throw new AuthenticationFailedException();
        }

        if (needsRehash)
        {
            user.PasswordHash = hasher.Hash(command.Password);
        }

        user.FailedSignIns = 0;
        user.LockedUntilUtc = null;
        user.LastSignInAtUtc = now;
        await db.SaveChangesAsync(cancellationToken);
        return await user.ToDtoAsync(db, cancellationToken);
    }
}
