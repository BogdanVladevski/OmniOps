namespace OmniOps.Core.Interfaces;

public interface IEmailSender
{
    /// <summary>False when no mail server is configured (messages are only logged).</summary>
    bool IsConfigured { get; }

    Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default);
}
