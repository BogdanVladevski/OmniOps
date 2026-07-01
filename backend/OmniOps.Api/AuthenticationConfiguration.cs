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

        var jwtOptions = builder.Configuration
            .GetSection(JwtOptions.SectionName)
            .Get<JwtOptions>() ?? new JwtOptions();

        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthorizationPolicies.VehicleRead, policy =>
                policy.AddRequirements(new ScopeRequirement(AuthorizationPolicies.VehicleReadScope)));

            options.AddPolicy(AuthorizationPolicies.VehicleSimulate, policy =>
                policy.AddRequirements(new ScopeRequirement(AuthorizationPolicies.VehicleSimulateScope)));
        });

        if (!jwtOptions.RequireAuthentication)
        {
            return;
        }

        if (!jwtOptions.IsConfigured())
        {
            throw new InvalidOperationException(
                "JWT_REQUIRE_AUTHENTICATION is enabled but JWT_SECRET is missing or shorter than 32 characters. " +
                "Set JWT_SECRET in .env or disable auth with JWT_REQUIRE_AUTHENTICATION=false for local development.");
        }

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidAudience = jwtOptions.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        System.Text.Encoding.UTF8.GetBytes(jwtOptions.Secret)),
                    ClockSkew = TimeSpan.FromMinutes(1)
                };

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;

                        if (!string.IsNullOrEmpty(accessToken)
                            && path.StartsWithSegments("/api/stream/telemetry"))
                        {
                            context.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    }
                };
            });
    }

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
        });
    }
}
