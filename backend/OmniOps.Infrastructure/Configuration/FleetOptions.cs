namespace OmniOps.Infrastructure.Configuration;

public class FleetOptions
{
    public const string SectionName = "Fleet";

    public string VehicleIds { get; set; } = "Truck-001,Truck-002,Truck-003";

    public string[] GetVehicleIdsArray()
    {
        if (string.IsNullOrWhiteSpace(VehicleIds))
        {
            return [];
        }

        return VehicleIds
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
