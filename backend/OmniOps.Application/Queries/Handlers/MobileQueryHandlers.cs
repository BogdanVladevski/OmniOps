using MediatR;
using OmniOps.Application.Dtos;
using OmniOps.Application.Queries;
using OmniOps.Core.Interfaces;

namespace OmniOps.Application.Queries.Handlers;

public class GetSyncSnapshotQueryHandler : IRequestHandler<GetSyncSnapshotQuery, SyncSnapshotDto>
{
    private readonly ISyncSnapshotService _sync;
    private readonly ITenantContext _tenant;

    public GetSyncSnapshotQueryHandler(ISyncSnapshotService sync, ITenantContext tenant)
    {
        _sync = sync;
        _tenant = tenant;
    }

    public async Task<SyncSnapshotDto> Handle(GetSyncSnapshotQuery request, CancellationToken cancellationToken)
    {
        var userId = _tenant.UserId ?? "anonymous";
        var snapshot = await _sync.GetSnapshotAsync(
            _tenant.OrganizationId, userId, request.SinceUtc, cancellationToken);

        return new SyncSnapshotDto(
            snapshot.ServerTimeUtc,
            snapshot.Vehicles.Cast<object>().ToList(),
            snapshot.Notifications.Select(NotificationDto.FromEntity).ToList());
    }
}
