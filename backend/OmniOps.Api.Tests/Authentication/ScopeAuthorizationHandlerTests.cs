using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using OmniOps.Api.Authentication;

namespace OmniOps.Api.Tests.Authentication;

public class ScopeAuthorizationHandlerTests
{
    private readonly ScopeAuthorizationHandler _handler = new();

    [Fact]
    public async Task HandleRequirementAsync_WithMatchingScope_Succeeds()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("scope", "vehicle:read vehicle:simulate")
        ], "Bearer"));

        var context = new AuthorizationHandlerContext(
            [new ScopeRequirement(AuthorizationPolicies.VehicleReadScope)],
            user,
            null);

        await _handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleRequirementAsync_WithoutMatchingScope_DoesNotSucceed()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("scope", "vehicle:simulate")
        ], "Bearer"));

        var context = new AuthorizationHandlerContext(
            [new ScopeRequirement(AuthorizationPolicies.VehicleReadScope)],
            user,
            null);

        await _handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }
}
