namespace OmniOps.Core.Interfaces;

public interface IFleetVehicleRegistry
{
    IReadOnlyList<string> GetConfiguredVehicleIds();
}
