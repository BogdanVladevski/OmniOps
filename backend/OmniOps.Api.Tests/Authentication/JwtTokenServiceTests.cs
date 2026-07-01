using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Options;
using OmniOps.Api.Authentication;
using OmniOps.Infrastructure.Configuration;

namespace OmniOps.Api.Tests.Authentication;

public class JwtTokenServiceTests
{
    [Fact]
    public void GenerateToken_WithValidScopes_ReturnsParseableJwt()
    {
        var service = CreateService();
        var token = service.GenerateToken([AuthorizationPolicies.VehicleReadScope]);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        Assert.Contains(jwt.Claims, c => c.Type == "scope" && c.Value.Contains("vehicle:read"));
        Assert.Equal("OmniOps", jwt.Issuer);
        Assert.Equal("OmniOps.Clients", jwt.Audiences.Single());
    }

    [Fact]
    public void GenerateToken_WithMultipleScopes_IncludesAllScopesInClaim()
    {
        var service = CreateService();
        var token = service.GenerateToken(
        [
            AuthorizationPolicies.VehicleReadScope,
            AuthorizationPolicies.VehicleSimulateScope
        ]);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);
        var scopeClaim = jwt.Claims.Single(c => c.Type == "scope").Value;

        Assert.Contains("vehicle:read", scopeClaim);
        Assert.Contains("vehicle:simulate", scopeClaim);
    }

    [Fact]
    public void GenerateToken_WhenSecretTooShort_ThrowsInvalidOperationException()
    {
        var service = new JwtTokenService(Options.Create(new JwtOptions
        {
            Secret = "too-short",
            Issuer = "OmniOps",
            Audience = "OmniOps.Clients"
        }));

        Assert.Throws<InvalidOperationException>(() =>
            service.GenerateToken([AuthorizationPolicies.VehicleReadScope]));
    }

    private static JwtTokenService CreateService() =>
        new(Options.Create(new JwtOptions
        {
            Secret = "test-secret-key-at-least-32-characters-long",
            Issuer = "OmniOps",
            Audience = "OmniOps.Clients",
            ExpirationMinutes = 60
        }));
}
