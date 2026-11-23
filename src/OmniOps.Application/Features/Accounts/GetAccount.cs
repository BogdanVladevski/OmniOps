using Mediator;
using Microsoft.EntityFrameworkCore;
using OmniOps.Application.Common.Interfaces;
using OmniOps.Application.Common.Security;

namespace OmniOps.Application.Features.Accounts;

/// <summary>Reloads an account by id (token refresh, "me"). Null if it no longer exists or was switched off.</summary>
[Anonymous]
public sealed record GetAccountQuery(Guid UserId) : IQuery<AccountDto?>;

public sealed class GetAccountHandler(IAppDbContext db) : IQueryHandler<GetAccountQuery, AccountDto?>
{
    public async ValueTask<AccountDto?> Handle(GetAccountQuery query, CancellationToken cancellationToken)
    {
        // Across companies: refresh tokens are presented without an access token, so no company is known yet.
        var user = await db.Users.AcrossCompanies().FirstOrDefaultAsync(u => u.Id == query.UserId, cancellationToken);
        return user is { IsActive: true } ? await user.ToDtoAsync(db, cancellationToken) : null;
    }
}
