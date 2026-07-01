using Microsoft.Extensions.Options;
using OmniOps.Core.Interfaces;
using OmniOps.Infrastructure.Configuration;

namespace OmniOps.Infrastructure.Services;

public class FleetVehicleRegistry : IFleetVehicleRegistry
{
    private readonly FleetOptions _options;

    public FleetVehicleRegistry(IOptions<FleetOptions> options)
    {
        _options = options.Value;
    }

    public IReadOnlyList<string> GetConfiguredVehicleIds() =>
        _options.GetVehicleIdsArray();
}
