namespace OmniOps.Core.Exceptions;

/// <summary>
/// The request is valid but not allowed in the current state (e.g. delivering a job that has not started).
/// Mapped to HTTP 409; the message is shown to the user, so keep it plain and free of internals.
/// </summary>
public class BusinessRuleException : Exception
{
    public BusinessRuleException(string message) : base(message)
    {
    }
}
