using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using OmniOps.Api.Authentication;
using OmniOps.Application.Common.Interfaces;
using OmniOps.Application.Common.Security;

namespace OmniOps.Api.Realtime;

/// <summary>
/// Live alerts and change hints for the office. Each connection joins its own company's group, taken from the sign-in and never from
/// the client, so one company never receives another's alerts.
/// </summary>
[Authorize(Policies.Office)]
public sealed class LiveHub : Hub
{
    public const string Path = "/hubs/live";

    public static string CompanyGroup(Guid organizationId) => $"company:{organizationId:N}";

    public override async Task OnConnectedAsync()
    {
        // From the connection's own sign-in: the request-scoped tenant context isn't reliable inside hub methods.
        if (Context.User?.GuidClaim(OmniClaims.Organization) is not { } organizationId)
        {
            Context.Abort();
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, CompanyGroup(organizationId));
        await base.OnConnectedAsync();
    }
}

/// <summary>
/// Messages the app receives: <c>alert</c> with { kind, title, message, jobId, at }, and <c>job-changed</c> with
/// { jobId } (refetch hint, no data, so nothing in it can go stale or leak).
/// </summary>
internal sealed partial class SignalRNotifier(IHubContext<LiveHub> hub, TimeProvider clock, ILogger<SignalRNotifier> logger) : IRealtimeNotifier
{
    public Task AlertAsync(Guid organizationId, string kind, string title, string message, Guid? jobId = null, CancellationToken cancellationToken = default) =>
        hub.Clients.Group(LiveHub.CompanyGroup(organizationId))
            .SendAsync("alert", new { kind, title, message, jobId, at = clock.GetUtcNow().UtcDateTime }, cancellationToken);

    public async Task JobChangedAsync(Guid organizationId, Guid jobId, CancellationToken cancellationToken = default)
    {
        try
        {
            await hub.Clients.Group(LiveHub.CompanyGroup(organizationId)).SendAsync("job-changed", new { jobId }, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogJobChangedFailed(logger, ex, jobId);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not announce a change to job {JobId}")]
    private static partial void LogJobChangedFailed(ILogger logger, Exception exception, Guid jobId);
}
