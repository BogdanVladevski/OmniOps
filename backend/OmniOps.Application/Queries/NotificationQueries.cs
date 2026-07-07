using MediatR;
using OmniOps.Application.Abstractions;
using OmniOps.Application.Dtos;
using OmniOps.Core.Authorization;

namespace OmniOps.Application.Queries;

public record GetNotificationsQuery(int Limit = 50) : IRequest<IReadOnlyList<NotificationDto>>, IAuthorizedRequest
{
    public string RequiredPolicy => AuthorizationPolicies.VehicleRead;
}

public record GetNotificationPreferencesQuery() : IRequest<IReadOnlyList<NotificationPreferenceDto>>, IAuthorizedRequest
{
    public string RequiredPolicy => AuthorizationPolicies.VehicleRead;
}

public record GetAlertRulesQuery() : IRequest<IReadOnlyList<AlertRuleDto>>, IAuthorizedRequest
{
    public string RequiredPolicy => AuthorizationPolicies.FleetAdmin;
}
