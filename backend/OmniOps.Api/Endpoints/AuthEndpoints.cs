using Microsoft.Extensions.Options;
using OmniOps.Api.Authentication;
using OmniOps.Infrastructure.Configuration;

namespace OmniOps.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        app.MapPost("/api/auth/token", IssueDevelopmentToken)
            .WithName("IssueDevelopmentToken")
            .WithOpenApi()
            .AllowAnonymous();
    }

    private static IResult IssueDevelopmentToken(
        TokenRequest request,
        IJwtTokenService tokenService,
        IOptions<JwtOptions> jwtOptions,
        IWebHostEnvironment environment)
    {
        if (!environment.IsDevelopment())
        {
            return Results.NotFound();
        }

        if (request.Scopes is null || request.Scopes.Count == 0)
        {
            return Results.BadRequest(new { Message = "At least one scope is required." });
        }

        var invalidScopes = request.Scopes
            .Where(scope => !IsKnownScope(scope))
            .ToList();

        if (invalidScopes.Count > 0)
        {
            return Results.BadRequest(new
            {
                Message = "Unknown scope(s).",
                InvalidScopes = invalidScopes,
                AllowedScopes = new[]
                {
                    AuthorizationPolicies.VehicleReadScope,
                    AuthorizationPolicies.VehicleSimulateScope
                }
            });
        }

        var token = tokenService.GenerateToken(request.Scopes);

        return Results.Ok(new TokenResponse
        {
            AccessToken = token,
            TokenType = "Bearer",
            ExpiresIn = jwtOptions.Value.ExpirationMinutes * 60,
            Scopes = request.Scopes
        });
    }

    private static bool IsKnownScope(string scope) =>
        scope is AuthorizationPolicies.VehicleReadScope
            or AuthorizationPolicies.VehicleSimulateScope;

    public sealed class TokenRequest
    {
        public List<string> Scopes { get; init; } = [];
    }

    private sealed class TokenResponse
    {
        public string AccessToken { get; init; } = string.Empty;
        public string TokenType { get; init; } = string.Empty;
        public int ExpiresIn { get; init; }
        public List<string> Scopes { get; init; } = [];
    }
}
