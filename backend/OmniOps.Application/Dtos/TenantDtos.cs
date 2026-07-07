using OmniOps.Core.Entities;

namespace OmniOps.Application.Dtos;

public record OrganizationDto(Guid Id, string Name, string Slug, DateTime CreatedAtUtc)
{
    public static OrganizationDto FromEntity(Organization o) => new(o.Id, o.Name, o.Slug, o.CreatedAtUtc);
}

public record WorkspaceDto(Guid Id, Guid OrganizationId, string Name, Guid? DefaultFleetId, DateTime CreatedAtUtc)
{
    public static WorkspaceDto FromEntity(Workspace w) =>
        new(w.Id, w.OrganizationId, w.Name, w.DefaultFleetId, w.CreatedAtUtc);
}

public record TeamDto(Guid Id, Guid OrganizationId, string Name, int MemberCount, DateTime CreatedAtUtc);

public record InvitationDto(Guid Id, string Email, string Role, DateTime ExpiresAtUtc, bool Accepted);

public record TenantSettingsDto(
    Guid OrganizationId,
    string TimeZone,
    string Locale,
    bool EmailNotificationsEnabled,
    bool PushNotificationsEnabled);
