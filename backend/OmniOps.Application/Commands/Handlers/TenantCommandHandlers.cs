using System.Security.Cryptography;
using System.Text;
using MediatR;
using OmniOps.Application.Commands;
using OmniOps.Application.Dtos;
using OmniOps.Core.Entities;
using OmniOps.Core.Interfaces;

namespace OmniOps.Application.Commands.Handlers;

public class CreateOrganizationCommandHandler : IRequestHandler<CreateOrganizationCommand, OrganizationDto>
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IAuditService _audit;
    private readonly ITenantContext _tenant;

    public CreateOrganizationCommandHandler(ITenantRepository tenantRepository, IAuditService audit, ITenantContext tenant)
    {
        _tenantRepository = tenantRepository;
        _audit = audit;
        _tenant = tenant;
    }

    public async Task<OrganizationDto> Handle(CreateOrganizationCommand request, CancellationToken cancellationToken)
    {
        var org = new Organization
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Slug = request.Slug.Trim().ToLowerInvariant(),
            CreatedAtUtc = DateTime.UtcNow
        };
        await _tenantRepository.AddOrganizationAsync(org, cancellationToken);
        await _tenantRepository.UpsertSettingsAsync(new TenantSettings { OrganizationId = org.Id }, cancellationToken);
        await _audit.LogAsync("Create", nameof(Organization), org.Id.ToString(), _tenant.UserId, org.Name, cancellationToken);
        await _tenantRepository.SaveChangesAsync(cancellationToken);
        return OrganizationDto.FromEntity(org);
    }
}

public class CreateWorkspaceCommandHandler : IRequestHandler<CreateWorkspaceCommand, WorkspaceDto>
{
    private readonly ITenantRepository _tenantRepository;
    private readonly ITenantContext _tenant;

    public CreateWorkspaceCommandHandler(ITenantRepository tenantRepository, ITenantContext tenant)
    {
        _tenantRepository = tenantRepository;
        _tenant = tenant;
    }

    public async Task<WorkspaceDto> Handle(CreateWorkspaceCommand request, CancellationToken cancellationToken)
    {
        if (request.OrganizationId != _tenant.OrganizationId)
            throw new UnauthorizedAccessException("Cross-tenant workspace creation is not allowed.");

        var workspace = new Workspace
        {
            Id = Guid.NewGuid(),
            OrganizationId = request.OrganizationId,
            Name = request.Name.Trim(),
            DefaultFleetId = request.DefaultFleetId,
            CreatedAtUtc = DateTime.UtcNow
        };
        await _tenantRepository.AddWorkspaceAsync(workspace, cancellationToken);
        await _tenantRepository.SaveChangesAsync(cancellationToken);
        return WorkspaceDto.FromEntity(workspace);
    }
}

public class CreateTeamCommandHandler : IRequestHandler<CreateTeamCommand, TeamDto>
{
    private readonly ITenantRepository _tenantRepository;
    private readonly ITenantContext _tenant;

    public CreateTeamCommandHandler(ITenantRepository tenantRepository, ITenantContext tenant)
    {
        _tenantRepository = tenantRepository;
        _tenant = tenant;
    }

    public async Task<TeamDto> Handle(CreateTeamCommand request, CancellationToken cancellationToken)
    {
        if (request.OrganizationId != _tenant.OrganizationId)
            throw new UnauthorizedAccessException("Cross-tenant team creation is not allowed.");

        var team = new Team
        {
            Id = Guid.NewGuid(),
            OrganizationId = request.OrganizationId,
            Name = request.Name.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };
        await _tenantRepository.AddTeamAsync(team, cancellationToken);
        await _tenantRepository.SaveChangesAsync(cancellationToken);
        return new TeamDto(team.Id, team.OrganizationId, team.Name, 0, team.CreatedAtUtc);
    }
}

public class InviteMemberCommandHandler : IRequestHandler<InviteMemberCommand, InvitationDto>
{
    private readonly ITenantRepository _tenantRepository;
    private readonly INotificationDispatcher _notifications;
    private readonly ITenantContext _tenant;

    public InviteMemberCommandHandler(
        ITenantRepository tenantRepository,
        INotificationDispatcher notifications,
        ITenantContext tenant)
    {
        _tenantRepository = tenantRepository;
        _notifications = notifications;
        _tenant = tenant;
    }

    public async Task<InvitationDto> Handle(InviteMemberCommand request, CancellationToken cancellationToken)
    {
        if (request.OrganizationId != _tenant.OrganizationId)
            throw new UnauthorizedAccessException("Cross-tenant invitation is not allowed.");

        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        var invitation = new Invitation
        {
            Id = Guid.NewGuid(),
            OrganizationId = request.OrganizationId,
            Email = request.Email.Trim().ToLowerInvariant(),
            Role = request.Role,
            TokenHash = hash,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7),
            CreatedAtUtc = DateTime.UtcNow
        };
        await _tenantRepository.AddInvitationAsync(invitation, cancellationToken);
        await _tenantRepository.SaveChangesAsync(cancellationToken);

        await _notifications.EnqueueIncidentAlertAsync(
            request.OrganizationId,
            request.Email,
            "Invitation",
            "OmniOps workspace invitation",
            $"You have been invited. Token: {token}",
            invitation.Id.ToString(),
            cancellationToken);

        return new InvitationDto(invitation.Id, invitation.Email, invitation.Role, invitation.ExpiresAtUtc, false);
    }
}

public class UpdateTenantSettingsCommandHandler : IRequestHandler<UpdateTenantSettingsCommand, TenantSettingsDto>
{
    private readonly ITenantRepository _tenantRepository;
    private readonly ITenantContext _tenant;

    public UpdateTenantSettingsCommandHandler(ITenantRepository tenantRepository, ITenantContext tenant)
    {
        _tenantRepository = tenantRepository;
        _tenant = tenant;
    }

    public async Task<TenantSettingsDto> Handle(UpdateTenantSettingsCommand request, CancellationToken cancellationToken)
    {
        if (request.OrganizationId != _tenant.OrganizationId)
            throw new UnauthorizedAccessException("Cross-tenant settings update is not allowed.");

        var settings = new TenantSettings
        {
            OrganizationId = request.OrganizationId,
            TimeZone = request.TimeZone,
            Locale = request.Locale,
            EmailNotificationsEnabled = request.EmailNotificationsEnabled,
            PushNotificationsEnabled = request.PushNotificationsEnabled
        };
        await _tenantRepository.UpsertSettingsAsync(settings, cancellationToken);
        await _tenantRepository.SaveChangesAsync(cancellationToken);
        return new TenantSettingsDto(
            settings.OrganizationId,
            settings.TimeZone,
            settings.Locale,
            settings.EmailNotificationsEnabled,
            settings.PushNotificationsEnabled);
    }
}
