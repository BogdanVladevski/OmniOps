namespace OmniOps.Core.Entities;

public static class TenantSeed
{
    public static readonly Guid DefaultOrganizationId = new("01000000-0000-0000-0000-000000000001");
    public static readonly Guid DefaultWorkspaceId = new("02000000-0000-0000-0000-000000000001");
}

public class Organization
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public ICollection<Workspace> Workspaces { get; set; } = new List<Workspace>();
    public ICollection<Team> Teams { get; set; } = new List<Team>();
}

public class Workspace
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid? DefaultFleetId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public Organization? Organization { get; set; }
}

public class Team
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public Organization? Organization { get; set; }
    public ICollection<TeamMember> Members { get; set; } = new List<TeamMember>();
}

public class TeamMember
{
    public Guid Id { get; set; }
    public Guid TeamId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Role { get; set; } = "member";
    public DateTime JoinedAtUtc { get; set; }
    public Team? Team { get; set; }
}

public class Invitation
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = "member";
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public bool Accepted { get; set; }
    public Organization? Organization { get; set; }
}

public class TenantSettings
{
    public Guid OrganizationId { get; set; }
    public string TimeZone { get; set; } = "UTC";
    public string Locale { get; set; } = "en-US";
    public bool EmailNotificationsEnabled { get; set; } = true;
    public bool PushNotificationsEnabled { get; set; } = true;
    public Organization? Organization { get; set; }
}
