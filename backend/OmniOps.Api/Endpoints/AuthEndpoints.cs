using Microsoft.Extensions.Options;
using OmniOps.Api.Authentication;
using OmniOps.Core.Entities;
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

        app.MapPost("/api/auth/refresh", RefreshToken)
            .WithName("RefreshToken")
            .WithOpenApi()
            .AllowAnonymous();
    }

    private static async Task<IResult> IssueDevelopmentToken(
        TokenRequest request,
        IJwtTokenService tokenService,
        IRefreshTokenService refreshTokenService,
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
                    AuthorizationPolicies.VehicleSimulateScope,
                    AuthorizationPolicies.FleetAdminScope,
                    AuthorizationPolicies.PlatformAdminScope
                }
            });
        }

        var token = tokenService.GenerateToken(request.Scopes, new TokenTenantContext(
            request.Subject ?? "dev-user",
            TenantSeed.DefaultOrganizationId,
            TenantSeed.DefaultWorkspaceId));
        var refreshToken = await refreshTokenService.IssueAsync(request.Subject ?? "dev-user", CancellationToken.None);

        return Results.Ok(new TokenResponse
        {
            AccessToken = token,
            RefreshToken = refreshToken,
            TokenType = "Bearer",
            ExpiresIn = jwtOptions.Value.ExpirationMinutes * 60,
            Scopes = request.Scopes
        });
    }

    private static async Task<IResult> RefreshToken(
        RefreshRequest request,
        IJwtTokenService tokenService,
        IRefreshTokenService refreshTokenService,
        IOptions<JwtOptions> jwtOptions,
        IWebHostEnvironment environment)
    {
        if (!environment.IsDevelopment()) return Results.NotFound();
        var rotated = await refreshTokenService.RotateAsync(request.RefreshToken, CancellationToken.None);
        if (rotated is null) return Results.Unauthorized();

        var accessToken = tokenService.GenerateToken(
            [AuthorizationPolicies.VehicleReadScope],
            new TokenTenantContext("dev-user", TenantSeed.DefaultOrganizationId, TenantSeed.DefaultWorkspaceId));
        return Results.Ok(new TokenResponse
        {
            AccessToken = accessToken,
            RefreshToken = rotated,
            TokenType = "Bearer",
            ExpiresIn = jwtOptions.Value.ExpirationMinutes * 60,
            Scopes = [AuthorizationPolicies.VehicleReadScope]
        });
    }

    private static bool IsKnownScope(string scope) =>
        scope is AuthorizationPolicies.VehicleReadScope
            or AuthorizationPolicies.VehicleSimulateScope
            or AuthorizationPolicies.FleetAdminScope
            or AuthorizationPolicies.PlatformAdminScope;

    public sealed class TokenRequest
    {
        public string? Subject { get; init; }
        public List<string> Scopes { get; init; } = [];
    }

    public sealed class RefreshRequest
    {
        public string RefreshToken { get; init; } = string.Empty;
    }

    private sealed class TokenResponse
    {
        public string AccessToken { get; init; } = string.Empty;
        public string RefreshToken { get; init; } = string.Empty;
        public string TokenType { get; init; } = string.Empty;
        public int ExpiresIn { get; init; }
        public List<string> Scopes { get; init; } = [];
    }
}
