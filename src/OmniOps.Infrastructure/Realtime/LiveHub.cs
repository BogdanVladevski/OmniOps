using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using OmniOps.Application.Common.Interfaces;
using OmniOps.Application.Common.Security;

namespace OmniOps.Infrastructure.Realtime;

/// <summary>
/// Live alerts for the office. Each connection joins its own company's group, taken from the sign-in and never from
/// the client, so one company never receives another's alerts.
/// </summary>
[Authorize(Policies.Office)]
public sealed class LiveHub(ITenantContext tenant) : Hub
{
    public const string Path = "/hubs/live";

    public static string CompanyGroup(Guid organizationId) => $"company:{organizationId:N}";

    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, CompanyGroup(tenant.OrganizationId));
        await base.OnConnectedAsync();
    }
}

/// <summary>Message the app receives: <c>alert</c> with { kind, title, message, jobId, at }.</summary>
internal sealed class SignalRNotifier(IHubContext<LiveHub> hub, TimeProvider clock) : IRealtimeNotifier
{
    public Task AlertAsync(Guid organizationId, string kind, string title, string message, Guid? jobId = null, CancellationToken cancellationToken = default) =>
        hub.Clients.Group(LiveHub.CompanyGroup(organizationId))
            .SendAsync("alert", new { kind, title, message, jobId, at = clock.GetUtcNow().UtcDateTime }, cancellationToken);
}
