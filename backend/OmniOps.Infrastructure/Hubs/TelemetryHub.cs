using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace OmniOps.Infrastructure.Hubs
{
    public class TelemetryHub : Hub
    {
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