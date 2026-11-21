using OmniOps.Core.Domain;
using OmniOps.Core.Entities;

namespace OmniOps.Application.Tests.Domain;

public class VehicleTenantTests
{
    [Fact]
    public void Unattributed_IsNotARealSeededTenant()
    {
        Assert.NotEqual(TenantSeed.DefaultOrganizationId, VehicleTenant.Unattributed.OrganizationId);
        Assert.NotEqual(TenantSeed.DefaultFleetId, VehicleTenant.Unattributed.FleetId);
        Assert.True(VehicleTenant.Unattributed.IsUnattributed);
    }
}
