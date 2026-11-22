using Microsoft.Extensions.Options;
using OmniOps.Application.Dtos;
using OmniOps.Core.Enums;
using OmniOps.Infrastructure.Configuration;

namespace OmniOps.Api.Authentication;

public record SessionDto(string AccessToken, string RefreshToken, int ExpiresIn, AccountDto Account);

/// <summary>Turns a signed-in account into tokens. What each role may do is decided here, from the account.</summary>
public class AccountTokenIssuer
{
    /// <summary>A driver's phone stays signed in for months; office sessions renew more often.</summary>
    private static readonly TimeSpan DriverSession = TimeSpan.FromDays(90);
    private static readonly TimeSpan OfficeSession = TimeSpan.FromDays(30);

    private readonly IJwtTokenService _tokens;
    private readonly IRefreshTokenService _refresh;
    private readonly JwtOptions _options;

    public AccountTokenIssuer(IJwtTokenService tokens, IRefreshTokenService refresh, IOptions<JwtOptions> options)
    {
        _tokens = tokens;
        _refresh = refresh;
        _options = options.Value;
    }

    public static IReadOnlyList<string> ScopesFor(string role, IEnumerable<string>? extra = null)
    {
        var scopes = Enum.Parse<UserRole>(role) switch
        {
            UserRole.Owner => new List<string> { AuthorizationPolicies.VehicleReadScope, AuthorizationPolicies.FleetAdminScope },
            UserRole.Dispatcher => [AuthorizationPolicies.VehicleReadScope],
            _ => [AuthorizationPolicies.DriverScope],
        };
        scopes.AddRange(extra ?? []);
        return scopes;
    }

    public async Task<SessionDto> IssueAsync(AccountDto account, IEnumerable<string>? extraScopes = null, CancellationToken cancellationToken = default)
    {
        var access = _tokens.GenerateToken(
            ScopesFor(account.Role, extraScopes),
            new TokenTenantContext(account.UserId.ToString(), account.OrganizationId, null, account.DriverId, account.Role, account.DisplayName));
        var lifetime = account.Role == nameof(UserRole.Driver) ? DriverSession : OfficeSession;
        var refresh = await _refresh.IssueAsync(account.UserId.ToString(), cancellationToken, lifetime);
        return new SessionDto(access, refresh, _options.ExpirationMinutes * 60, account);
    }
}
