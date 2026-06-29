using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace OmniOps.Api.Hubs
{
    public class TelemetryHub : Hub
    {
        // Clients can call this to join a specific vehicle's tracking stream group
        public async Task WatchVehicle(string vehicleId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, vehicleId);
        }

        public async Task UnwatchVehicle(string vehicleId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, vehicleId);
        }
    }
}