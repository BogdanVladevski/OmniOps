using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OmniOps.Api.Realtime;
using OmniOps.Application.Common.Interfaces;
using OmniOps.Application.Common.Security;
using OmniOps.Domain.Accounts;

namespace OmniOps.Api.Authentication;

internal static class AuthenticationSetup
{
    public static IServiceCollection AddOmniAuthentication(this IServiceCollection services)
    {
        var jwt = services.AddOptions<JwtOptions>().BindConfiguration(JwtOptions.SectionName).ValidateDataAnnotations();
        if (!Infrastructure.BuildContext.IsGeneratingOpenApiDocument)
        {
            jwt.ValidateOnStart();
        }

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        // Configured from options when the app runs, so test and host configuration (applied at Build) are honoured.
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme).Configure<IOptions<JwtOptions>>((bearer, jwt) =>
        {
            bearer.MapInboundClaims = false;
            bearer.TokenValidationParameters = new TokenValidationParameters
            {
                ValidIssuer = jwt.Value.Issuer,
                ValidAudience = jwt.Value.Audience,
                IssuerSigningKey = TokenService.SigningKey(jwt.Value),
                RoleClaimType = OmniClaims.Role,
                NameClaimType = OmniClaims.Name,
                ClockSkew = TimeSpan.FromMinutes(1),
            };
            bearer.Events = new JwtBearerEvents
            {
                // Browsers can't set headers on WebSockets: SignalR sends the token in the query string.
                OnMessageReceived = context =>
                {
                    if (context.Request.Path.StartsWithSegments(LiveHub.Path) && context.Request.Query["access_token"] is { Count: > 0 } token)
                    {
                        context.Token = token;
                    }

                    return Task.CompletedTask;
                },

                // A token stops working as soon as its person is removed or their sessions are ended.
                OnTokenValidated = async context =>
                {
                    var user = context.Principal;
                    if (user.GuidClaim(OmniClaims.Subject) is not { } userId
                        || !long.TryParse(user?.FindFirst(OmniClaims.IssuedAtMs)?.Value, out var issuedMs)
                        || !await context.HttpContext.RequestServices.GetRequiredService<IAccountSessions>()
                            .IsCurrentAsync(userId, DateTimeOffset.FromUnixTimeMilliseconds(issuedMs).UtcDateTime, context.HttpContext.RequestAborted))
                    {
                        context.Fail("This sign-in has ended.");
                    }
                },
            };
        });

        services.AddAuthorizationBuilder()
            .AddPolicy(Policies.Owner, p => p.RequireRole(nameof(UserRole.Owner)))
            .AddPolicy(Policies.Office, p => p.RequireRole(nameof(UserRole.Owner), nameof(UserRole.Dispatcher)))
            .AddPolicy(Policies.JobWork, p => p.RequireRole(nameof(UserRole.Owner), nameof(UserRole.Dispatcher), nameof(UserRole.Driver)));

        services.AddHttpContextAccessor();
        services.AddScoped<ITenantContext, HttpTenantContext>();
        services.AddScoped<IPolicyEnforcer, HttpPolicyEnforcer>();
        services.AddScoped<TokenService>();
        return services;
    }
}
