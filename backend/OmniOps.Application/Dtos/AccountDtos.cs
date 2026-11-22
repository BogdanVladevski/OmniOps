namespace OmniOps.Application.Dtos;

/// <summary>Who is signed in. The API turns this into tokens; the app uses it to choose office or driver mode.</summary>
public record AccountDto(
    Guid UserId,
    string DisplayName,
    string? Email,
    string Role,
    Guid OrganizationId,
    string OrganizationName,
    Guid? DefaultFleetId,
    Guid? DriverId);

public record DriverPairingCodeDto(string Code, DateTime ExpiresAtUtc, string DriverName);
