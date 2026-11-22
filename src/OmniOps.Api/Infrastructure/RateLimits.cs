using System.Threading.RateLimiting;

namespace OmniOps.Api.Infrastructure;

internal static class RateLimits
{
    /// <summary>Sign-in, sign-up, codes and links: per client address, against guessing.</summary>
    public const string Accounts = "accounts";

    /// <summary>Public tracking and invoice pages: per client address.</summary>
    public const string Public = "public";

    public static IServiceCollection AddOmniRateLimits(this IServiceCollection services) =>
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(Accounts, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = context.RequestServices.GetRequiredService<IConfiguration>().GetValue("RateLimits:AccountsPerMinute", 10),
                    Window = TimeSpan.FromMinutes(1),
                }));
            options.AddPolicy(Public, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 120, Window = TimeSpan.FromMinutes(1) }));
        });
}
