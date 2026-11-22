namespace OmniOps.Core.Interfaces;

/// <summary>Absolute links to the public pages, for emails (the API's public base URL).</summary>
public interface IPublicLinks
{
    string Invoice(string token);
    string Tracking(string token);
}
