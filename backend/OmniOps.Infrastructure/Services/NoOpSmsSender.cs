using OmniOps.Core.Interfaces;

namespace OmniOps.Infrastructure.Services;

public class NoOpSmsSender : ISmsSender
{
    public Task SendAsync(string phoneNumber, string message, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
