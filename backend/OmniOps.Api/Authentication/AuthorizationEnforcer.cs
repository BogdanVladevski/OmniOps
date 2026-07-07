using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using OmniOps.Core.Interfaces;
using OmniOps.Infrastructure.Configuration;

namespace OmniOps.Api.Authentication;

public class AuthorizationEnforcer : IAuthorizationEnforcer
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly JwtOptions _jwtOptions;

    public AuthorizationEnforcer(
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor,
        IOptions<JwtOptions> jwtOptions)
    {
        _authorizationService = authorizationService;
        _httpContextAccessor = httpContextAccessor;
        _jwtOptions = jwtOptions.Value;
    }

    public async Task EnforcePolicyAsync(string policyName, CancellationToken cancellationToken = default)
    {
        if (!_jwtOptions.RequireAuthentication)
        {
            return;
        }

        var user = _httpContextAccessor.HttpContext?.User;
        if (user is null)
        {
            throw new UnauthorizedAccessException("No HTTP context available for authorization.");
        }

        var result = await _authorizationService.AuthorizeAsync(user, policyName);
        if (!result.Succeeded)
        {
            throw new UnauthorizedAccessException($"Authorization policy '{policyName}' was not satisfied.");
        }
    }
}
