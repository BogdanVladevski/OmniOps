namespace OmniOps.Core.Authorization;

public static class AuthorizationPolicies
{
    public const string VehicleRead = "VehicleRead";
    public const string VehicleSimulate = "VehicleSimulate";
    public const string FleetAdmin = "FleetAdmin";
    public const string PlatformAdmin = "PlatformAdmin";

    public const string VehicleReadScope = "vehicle:read";
    public const string VehicleSimulateScope = "vehicle:simulate";
    public const string FleetAdminScope = "fleet:admin";
    public const string PlatformAdminScope = "platform:admin";
}
