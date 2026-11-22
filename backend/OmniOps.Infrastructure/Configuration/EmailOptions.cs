namespace OmniOps.Infrastructure.Configuration;

/// <summary>Outgoing mail. Without SmtpHost the app only logs emails (and says so in the UI).</summary>
public class EmailOptions
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

public class PublicUrlOptions
{
    public const string SectionName = "PublicUrls";

    /// <summary>Base URL customers use to reach this API's public pages (tracking, invoices).</summary>
    public string BaseUrl { get; set; } = "http://localhost:5031";

    /// <summary>Where the app runs, for links that open it (invitations, password resets).</summary>
    public string AppUrl { get; set; } = "http://localhost:19006";
}

public class InvoiceReminderOptions
{
    public const string SectionName = "InvoiceReminders";

    public int IntervalMinutes { get; set; } = 60;
}
