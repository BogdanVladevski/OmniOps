namespace OmniOps.Application.Dtos;

public record SyncSnapshotDto(
    DateTime ServerTimeUtc,
    IReadOnlyList<object> Vehicles,
    IReadOnlyList<NotificationDto> Notifications);
