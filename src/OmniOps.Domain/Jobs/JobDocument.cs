namespace OmniOps.Domain.Jobs;

/// <summary>A file attached to a delivery job: photos, loading note (CMR), temperature photos, signature, problem reports.</summary>
public class JobDocument
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid JobId { get; set; }
    public JobDocumentKind Kind { get; set; }

    /// <summary>Key in the file store; never exposed to clients.</summary>
    public string StorageKey { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTime CapturedAtUtc { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    /// <summary>For temperature photos: the reading in °C the driver entered from the display.</summary>
    public double? TemperatureCelsius { get; set; }

    /// <summary>Signature: the signer's name. Exception: what is wrong (e.g. "2 cartons crushed").</summary>
    public string? Note { get; set; }

    /// <summary>
    /// Id the phone gave this upload. Phones resend after a lost connection; the same id is stored once.
    /// </summary>
    public Guid? ClientId { get; set; }

    /// <summary>When the server received it (CapturedAtUtc is when it was taken, possibly offline).</summary>
    public DateTime? ReceivedAtUtc { get; set; }
    public Job Job { get; set; } = null!;
}
