namespace OmniOps.Core.Interfaces;

public interface ILoginHistoryService
{
    Task RecordAsync(string userId, string? ipAddress, bool success, CancellationToken cancellationToken = default);
}
