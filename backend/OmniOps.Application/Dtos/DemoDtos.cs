namespace OmniOps.Application.Dtos;

public record DemoStatusDto(
    string OrganizationName,
    Guid OrganizationId,
    Guid DefaultFleetId,
    string FleetName,
    int VehicleCount,
    int OpenIncidents,
    bool IsDemoOrganization);

public record DemoBootstrapDto(
    int VehiclesSimulated,
    int TotalPacketsQueued,
    string Message);
