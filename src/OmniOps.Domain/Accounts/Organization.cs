namespace OmniOps.Domain.Accounts;

/// <summary>A company using OmniOps. Everything else (trucks, drivers, jobs, invoices, people) belongs to one.</summary>
public class Organization
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>URL-safe, unique short name derived from the company name.</summary>
    public string Slug { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }
}
