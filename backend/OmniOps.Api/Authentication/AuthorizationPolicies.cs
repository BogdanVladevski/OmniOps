namespace OmniOps.Api.Authentication;

public static class AuthorizationPolicies
{
    public const string VehicleRead = "VehicleRead";
    public const string VehicleSimulate = "VehicleSimulate";

    public const string VehicleReadScope = "vehicle:read";
    public const string VehicleSimulateScope = "vehicle:simulate";
}
