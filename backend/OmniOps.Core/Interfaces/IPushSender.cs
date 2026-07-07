namespace OmniOps.Core.Interfaces;

public interface IPushSender
{
    Task SendAsync(string pushToken, string title, string body, CancellationToken cancellationToken = default);
}
