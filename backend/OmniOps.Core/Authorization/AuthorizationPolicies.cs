namespace OmniOps.Core.Authorization;

public static class AuthorizationPolicies
{
    public const string VehicleRead = "VehicleRead";
    public const string VehicleSimulate = "VehicleSimulate";
    public const string FleetAdmin = "FleetAdmin";
    public const string PlatformAdmin = "PlatformAdmin";

    /// <summary>Job work done from a driver's phone or by the office (start, GPS, photos, delivery).</summary>
    public const string JobWork = "JobWork";

    /// <summary>Scope held only by driver sign-ins.</summary>
    public const string DriverScope = "driver:app";

    public const string VehicleReadScope = "vehicle:read";
    public const string VehicleSimulateScope = "vehicle:simulate";
    public const string FleetAdminScope = "fleet:admin";
    public const string PlatformAdminScope = "platform:admin";
}
