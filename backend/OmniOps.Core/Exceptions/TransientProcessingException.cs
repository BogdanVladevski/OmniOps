namespace OmniOps.Core.Exceptions;

/// <summary>
/// Signals a retryable infrastructure failure (e.g. transient DB outage) where the packet
/// should be reprocessed rather than routed to the DLQ as poison data.
/// </summary>
public sealed class TransientProcessingException : Exception
{
    public TransientProcessingException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
