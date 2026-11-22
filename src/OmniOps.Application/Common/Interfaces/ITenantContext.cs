namespace OmniOps.Application.Common.Interfaces;

/// <summary>Who is calling: their company, their user id and, for drivers, which driver they are.</summary>
public interface ITenantContext
{
    /// <summary>The signed-in company. Throws if nobody is signed in.</summary>
    Guid OrganizationId { get; }

    /// <summary>The signed-in company, or null for anonymous calls (public links, sign-in).</summary>
    Guid? CurrentOrganizationId { get; }

    Guid? UserId { get; }

    /// <summary>Set for driver sign-ins: they only ever see this driver's jobs.</summary>
    Guid? DriverId { get; }
}
