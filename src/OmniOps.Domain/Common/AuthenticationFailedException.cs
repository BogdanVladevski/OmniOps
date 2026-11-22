namespace OmniOps.Domain.Common;

/// <summary>Sign-in failed. Mapped to 401; the message is shown as is, so it never says which part was wrong.</summary>
public class AuthenticationFailedException : Exception
{
    public AuthenticationFailedException(string message = "Email or password is not right.") : base(message)
    {
    }
}
