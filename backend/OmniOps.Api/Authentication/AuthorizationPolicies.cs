namespace OmniOps.Api.Authentication;

public static class AuthorizationPolicies
{
    public const string VehicleRead = Core.Authorization.AuthorizationPolicies.VehicleRead;
    public const string VehicleSimulate = Core.Authorization.AuthorizationPolicies.VehicleSimulate;
    public const string FleetAdmin = Core.Authorization.AuthorizationPolicies.FleetAdmin;
    public const string PlatformAdmin = Core.Authorization.AuthorizationPolicies.PlatformAdmin;
    public const string VehicleReadScope = Core.Authorization.AuthorizationPolicies.VehicleReadScope;
    public const string VehicleSimulateScope = Core.Authorization.AuthorizationPolicies.VehicleSimulateScope;
    public const string FleetAdminScope = Core.Authorization.AuthorizationPolicies.FleetAdminScope;
    public const string PlatformAdminScope = Core.Authorization.AuthorizationPolicies.PlatformAdminScope;
}
