namespace OmniOps.Infrastructure.Email;

/// <summary>Outgoing mail. Without <see cref="SmtpHost"/> emails are only logged (and the app says so).</summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string? SmtpHost { get; set; }
    public int SmtpPort { get; set; } = 587;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public bool EnableSsl { get; set; } = true;

    /// <summary>Sender address, e.g. "Vardar Transport &lt;invoices@example.com&gt;".</summary>
    public string? From { get; set; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(SmtpHost) && !string.IsNullOrWhiteSpace(From);
}

/// <summary>Where customers and teammates reach the product, for links in emails and messages.</summary>
public sealed class PublicUrlOptions
{
    public const string SectionName = "PublicUrls";

    /// <summary>Base URL of this API's public pages (tracking, invoices).</summary>
    public string ApiUrl { get; set; } = "http://localhost:5031";

    /// <summary>Where the app runs (invitations, password resets open it).</summary>
    public string AppUrl { get; set; } = "http://localhost:8081";
}
