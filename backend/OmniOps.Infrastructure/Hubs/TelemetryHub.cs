using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace OmniOps.Infrastructure.Hubs
{
    public class TelemetryHub : Hub
    {
        public const string FleetGroupName = "fleet";

        public async Task WatchVehicle(string vehicleId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, vehicleId);
        }

        public async Task UnwatchVehicle(string vehicleId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, vehicleId);
        }

        public async Task WatchFleet()
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, FleetGroupName);
        }

        public async Task UnwatchFleet()
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, FleetGroupName);
        }
    }
}