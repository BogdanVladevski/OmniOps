using MediatR;
using OmniOps.Application.Commands;
using OmniOps.Core.Entities;
using OmniOps.Core.Interfaces;

namespace OmniOps.Application.Commands.Handlers;

public class RegisterPushTokenCommandHandler : IRequestHandler<RegisterPushTokenCommand, bool>
{
    private readonly IDeviceRegistrationRepository _devices;
    private readonly ITenantContext _tenant;

    public RegisterPushTokenCommandHandler(IDeviceRegistrationRepository devices, ITenantContext tenant)
    {
        _devices = devices;
        _tenant = tenant;
    }

    public async Task<bool> Handle(RegisterPushTokenCommand request, CancellationToken cancellationToken)
    {
        await _devices.UpsertAsync(new DeviceRegistration
        {
            Id = Guid.NewGuid(),
            UserId = _tenant.UserId ?? "anonymous",
            OrganizationId = _tenant.OrganizationId,
            PushToken = request.PushToken,
            Platform = request.Platform,
            RegisteredAtUtc = DateTime.UtcNow,
            LastSeenAtUtc = DateTime.UtcNow
        }, cancellationToken);
        await _devices.SaveChangesAsync(cancellationToken);
        return true;
    }
}
