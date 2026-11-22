using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OmniOps.Api.Authentication;
using OmniOps.Infrastructure.Configuration;

namespace OmniOps.Api;

public static partial class ServiceRegistration
{
    private static void ConfigureAuthentication(WebApplicationBuilder builder)
    {
        builder.Services.Configure<JwtOptions>(
            builder.Configuration.GetSection(JwtOptions.SectionName));

        builder.Services.AddSingleton<IAuthorizationHandler, ScopeAuthorizationHandler>();
        builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();

        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthorizationPolicies.VehicleRead, policy =>
                policy.AddRequirements(new ScopeRequirement(AuthorizationPolicies.VehicleReadScope)));

            options.AddPolicy(AuthorizationPolicies.VehicleSimulate, policy =>
                policy.AddRequirements(new ScopeRequirement(AuthorizationPolicies.VehicleSimulateScope)));

            options.AddPolicy(AuthorizationPolicies.FleetAdmin, policy =>
                policy.AddRequirements(new ScopeRequirement(AuthorizationPolicies.FleetAdminScope)));

            options.AddPolicy(AuthorizationPolicies.PlatformAdmin, policy =>
                policy.AddRequirements(new ScopeRequirement(AuthorizationPolicies.PlatformAdminScope)));

            // Job actions from a driver's phone or the office.
            options.AddPolicy(AuthorizationPolicies.JobWork, policy =>
                policy.AddRequirements(new ScopeRequirement(AuthorizationPolicies.VehicleReadScope, AuthorizationPolicies.DriverScope)));
        });

        // Always registered; settings are read from IOptions when the app runs (not here), so host and test
        // configuration — applied at Build() — decide whether sign-in is enforced and with which key.
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((options, jwt) =>
            {
                var settings = jwt.Value;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = settings.Issuer,
                    ValidAudience = settings.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(
                        settings.IsConfigured() ? settings.Secret : new string('x', 32))),
                    ClockSkew = TimeSpan.FromMinutes(1),
                };
                options.Events = new JwtBearerEvents
                {
                    // SignalR sends the token in the query string (WebSockets cannot set headers).
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        if (!string.IsNullOrEmpty(accessToken) && context.HttpContext.Request.Path.StartsWithSegments("/api/stream/telemetry"))
                        {
                            context.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    },

                    // Account tokens stop working as soon as the person is removed or their sessions are ended.
                    OnTokenValidated = async context =>
                    {
                        var principal = context.Principal;
                        if ((principal?.FindFirst("role") ?? principal?.FindFirst(System.Security.Claims.ClaimTypes.Role)) is null
                            || !Guid.TryParse(principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                                ?? principal.FindFirst("sub")?.Value, out var userId))
                        {
                            return;
                        }

                        var issuedAt = long.TryParse(principal.FindFirst(JwtTokenService.IssuedAtMsClaim)?.Value, out var ms)
                            ? DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime
                            : DateTime.MinValue;
                        var sessions = context.HttpContext.RequestServices.GetRequiredService<OmniOps.Core.Interfaces.IAccountSessions>();
                        if (!await sessions.IsCurrentAsync(userId, issuedAt, context.HttpContext.RequestAborted))
                        {
                            context.Fail("This sign-in has ended.");
                        }
                    },
                };
            });
    }

    public const string PublicTrackingPolicy = "public-tracking";
    public const string AccountPolicy = "account";

    private static void ConfigureRateLimiting(WebApplicationBuilder builder)
    {
        var rateLimitOptions = builder.Configuration
            .GetSection(SimulateRateLimitOptions.SectionName)
            .Get<SimulateRateLimitOptions>() ?? new SimulateRateLimitOptions();

        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy("simulate", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = rateLimitOptions.PermitLimit,
                        Window = TimeSpan.FromSeconds(rateLimitOptions.WindowSeconds),
                        QueueLimit = 0
                    }));

            // Sign-up, sign-in and driver codes: slow down password and code guessing.
            options.AddPolicy(AccountPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        // Read per request so host/test configuration applies.
                        PermitLimit = httpContext.RequestServices.GetRequiredService<IConfiguration>()
                            .GetValue("AccountRateLimit:PermitLimit", 10),
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));

            // Public tracking links are anonymous; a page polls about twice a minute.
            options.AddPolicy(PublicTrackingPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 60,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));
        });
    }
}
