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
    int JobsCreated,
    int TrucksOnTheWay,
    int LocationPoints,
    string Message);
