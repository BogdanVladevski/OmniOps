using MediatR;
using Microsoft.Extensions.Options;
using OmniOps.Api.Authentication;
using OmniOps.Application.Commands;
using OmniOps.Application.Queries;
using OmniOps.Infrastructure.Configuration;

namespace OmniOps.Api.Endpoints;

public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this WebApplication app)
    {
        var requireAuth = app.Services.GetRequiredService<IOptions<JwtOptions>>().Value.RequireAuthentication;
        var v1 = app.MapGroup("/api/v1/admin").WithTags("Admin");

        MapGet(v1, "/audit-logs", async (DateTime? fromUtc, DateTime? toUtc, string? entityType, int? limit, IMediator m) =>
            Results.Ok(await m.Send(new GetAuditLogsQuery(fromUtc, toUtc, entityType, limit ?? 100))), requireAuth);

        MapGet(v1, "/api-keys", async (IMediator m) =>
            Results.Ok(await m.Send(new GetApiKeysQuery())), requireAuth);

        MapPost(v1, "/api-keys", async (ApiKeyBody body, IMediator m) =>
            Results.Ok(await m.Send(new CreateApiKeyCommand(body.Name, body.Scopes, body.ExpiresInDays))), requireAuth);

        MapDelete(v1, "/api-keys/{apiKeyId:guid}", async (Guid apiKeyId, IMediator m) =>
        {
            await m.Send(new RevokeApiKeyCommand(apiKeyId));
            return Results.NoContent();
        }, requireAuth);
    }

    private static void MapGet(RouteGroupBuilder group, string pattern, Delegate handler, bool requireAuth)
    {
        var ep = group.MapGet(pattern, handler).WithOpenApi();
        if (requireAuth) ep.RequireAuthorization(AuthorizationPolicies.PlatformAdmin);
    }

    private static void MapPost(RouteGroupBuilder group, string pattern, Delegate handler, bool requireAuth)
    {
        var ep = group.MapPost(pattern, handler).WithOpenApi();
        if (requireAuth) ep.RequireAuthorization(AuthorizationPolicies.PlatformAdmin);
    }

    private static void MapDelete(RouteGroupBuilder group, string pattern, Delegate handler, bool requireAuth)
    {
        var ep = group.MapDelete(pattern, handler).WithOpenApi();
        if (requireAuth) ep.RequireAuthorization(AuthorizationPolicies.PlatformAdmin);
    }

    private sealed record ApiKeyBody(string Name, string Scopes, int? ExpiresInDays);
}
