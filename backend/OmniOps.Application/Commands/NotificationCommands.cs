using MediatR;
using OmniOps.Application.Abstractions;
using OmniOps.Application.Dtos;
using OmniOps.Core.Authorization;

namespace OmniOps.Application.Commands;

public record UpdateNotificationPreferencesCommand(
    string AlertType,
    bool EmailEnabled,
    bool PushEnabled,
    bool SmsEnabled,
    bool InAppEnabled) : IRequest<NotificationPreferenceDto>, IAuthorizedRequest
{
    public string RequiredPolicy => AuthorizationPolicies.VehicleRead;
}

public record CreateAlertRuleCommand(
    string Name,
    string AlertType,
    string Severity,
    bool NotifyEmail,
    bool NotifyPush) : IRequest<AlertRuleDto>, IAuthorizedRequest, ITransactionalRequest
{
    public string RequiredPolicy => AuthorizationPolicies.FleetAdmin;
}
