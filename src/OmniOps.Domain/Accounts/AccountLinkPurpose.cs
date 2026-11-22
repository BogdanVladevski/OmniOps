namespace OmniOps.Domain.Accounts;

/// <summary>Stored as integers: never renumber.</summary>
public enum AccountLinkPurpose
{
    Invitation = 0,
    PasswordReset = 1,
}
