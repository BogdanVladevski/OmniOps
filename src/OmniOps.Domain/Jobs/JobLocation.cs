namespace OmniOps.Domain.Jobs;

/// <summary>
/// One GPS reading from the driver's phone during a job: the route the office and customer see, and the evidence
/// behind arrival times. The id comes from the phone, so a resent reading is stored once.
/// </summary>
public class JobLocation
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    public Guid OrganizationId { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double? SpeedKmh { get; set; }
    public double? Heading { get; set; }
    public double? AccuracyMeters { get; set; }
    public DateTime RecordedAtUtc { get; set; }
}
