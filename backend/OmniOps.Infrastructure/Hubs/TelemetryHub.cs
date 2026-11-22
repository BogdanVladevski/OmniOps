using Microsoft.AspNetCore.SignalR;
using OmniOps.Core.Interfaces;

namespace OmniOps.Infrastructure.Hubs;

/// <summary>
/// Live updates. Clients only ever join groups of their own organization (taken from their sign-in,
/// never from the client), so one company never receives another company's trucks or alerts.
/// </summary>
public class TelemetryHub : Hub
{
    private readonly ITenantContext _tenant;

    public TelemetryHub(ITenantContext tenant) => _tenant = tenant;

    public static string OrganizationGroup(Guid organizationId) => $"org:{organizationId:N}";

    public static string VehicleGroup(Guid organizationId, string vehicleId) => $"org:{organizationId:N}:vehicle:{vehicleId}";

    public Task WatchFleet() =>
        Groups.AddToGroupAsync(Context.ConnectionId, OrganizationGroup(_tenant.OrganizationId));

    public Task UnwatchFleet() =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, OrganizationGroup(_tenant.OrganizationId));

    public Task WatchVehicle(string vehicleId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, VehicleGroup(_tenant.OrganizationId, vehicleId));

    public Task UnwatchVehicle(string vehicleId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, VehicleGroup(_tenant.OrganizationId, vehicleId));
}
