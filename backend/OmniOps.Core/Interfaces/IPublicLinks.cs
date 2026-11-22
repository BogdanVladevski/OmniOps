namespace OmniOps.Core.Interfaces;

/// <summary>Absolute links to the public pages, for emails (the API's public base URL).</summary>
public interface IPublicLinks
{
    string Invoice(string token);
    string Tracking(string token);

    /// <summary>Opens the app to accept an invitation to a company.</summary>
    string Join(string token);

    /// <summary>Opens the app to choose a new password.</summary>
    string PasswordReset(string token);
}
