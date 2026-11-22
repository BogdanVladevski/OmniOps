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

public record TeamMemberDto(
    Guid UserId, string DisplayName, string? Email, string Role, bool IsActive, DateTime? LastSignInAtUtc, bool IsYou);

/// <summary>An invitation that has not been accepted yet. The link is only known when it is created.</summary>
public record TeamInvitationDto(Guid Id, string Email, string Role, DateTime ExpiresAtUtc, string? Link, bool Emailed);

public record TeamOverviewDto(IReadOnlyList<TeamMemberDto> Members, IReadOnlyList<TeamInvitationDto> Invitations, bool EmailConfigured);

/// <summary>What the person opening an invitation link sees before choosing a password.</summary>
public record InvitationPreviewDto(string CompanyName, string Email, string Role);

/// <summary>A one-time link (password reset) to hand over when no email could be sent.</summary>
public record AccountLinkDto(string Link, DateTime ExpiresAtUtc, bool Emailed);

public record CompanyDto(Guid OrganizationId, string Name);
