using Mediator;
using Microsoft.EntityFrameworkCore;
using OmniOps.Application.Common.Interfaces;
using OmniOps.Application.Common.Security;
using OmniOps.Domain.Jobs;

namespace OmniOps.Application.Features.Jobs;

// The proof pack: one PDF per delivery with everything a customer, an auditor or a claims handler asks for. Times are
// written in the carrier's time zone. Nothing about the driver as a person; the truck's plate is evidence.

public sealed record ProofPack(
    string CompanyName,
    string? CompanyDetails,
    string CustomerName,
    string? Reference,
    string? Origin,
    string? Destination,
    string Truck,
    string Status,
    string? ReceivedBy,
    string? DeliveryNote,
    string? TemperatureRange,
    IReadOnlyList<ProofEvent> Timeline,
    IReadOnlyList<ProofWait> Waits,
    IReadOnlyList<ProofReading> Readings,
    IReadOnlyList<ProofImage> Images,
    string TimeZone,
    DateTime GeneratedAt);

/// <summary>A step of the delivery, in the carrier's local time.</summary>
public sealed record ProofEvent(string Label, DateTime At);

public sealed record ProofWait(string Stop, DateTime ArrivedAt, DateTime? ReleasedAt, int WaitedMinutes, int ChargedMinutes, string Evidence);

public sealed record ProofReading(DateTime At, double Celsius, bool OutOfRange);

public sealed record ProofImage(string Title, string Caption, byte[] Content);

public sealed record PdfFile(byte[] Content, string FileName);

internal static class ProofPacks
{
    /// <summary>Photos in a pack; a delivery rarely has more, and this keeps the file a sensible size.</summary>
    private const int MaxImages = 40;

    // What the PDF engine can embed; other formats (e.g. HEIC) are left out of the pack, not out of the record.
    private static readonly string[] Formats = ["image/jpeg", "image/png", "image/webp"];

    /// <summary><paramref name="job"/> must include its vehicle and documents.</summary>
    public static async Task<PdfFile> RenderAsync(
        IAppDbContext db, IFileStore files, IProofPackRenderer renderer, Job job, DateTime nowUtc, CancellationToken ct)
    {
        var company = await db.Organizations.Where(o => o.Id == job.OrganizationId).Select(o => o.Name).FirstAsync(ct);
        var profile = await db.BillingProfiles.AcrossCompanies().FirstOrDefaultAsync(p => p.OrganizationId == job.OrganizationId, ct);
        var terms = WaitingTerms.From(profile);
        DateTime Local(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), terms.Zone);

        var timeline = new List<ProofEvent> { new("Job created", Local(job.CreatedAtUtc)) };
        void Add(string label, DateTime? at)
        {
            if (at is { } value)
            {
                timeline.Add(new ProofEvent(label, Local(value)));
            }
        }

        Add("Arrived at pickup", job.PickupArrivedAtUtc);
        Add("Left for the customer", job.StartedAtUtc);
        Add("Arrived at the customer", job.DeliveryArrivedAtUtc);
        Add(job.RecipientName is null ? "Delivered" : $"Delivered, received by {job.RecipientName}", job.DeliveredAtUtc);

        var waits = WaitingTimePolicy.Waits(job, terms.FreeMinutes, terms.RatePerHour, nowUtc)
            .Select(w => new ProofWait(
                w.Stop == WaitingTimePolicy.Pickup ? "Loading" : "Unloading",
                Local(w.ArrivedAtUtc), w.ReleasedAtUtc is { } r ? Local(r) : null, w.WaitedMinutes, w.ChargedMinutes, Evidence(w)))
            .ToList();

        var documents = job.Documents.OrderBy(d => d.CapturedAtUtc).ToList();
        var readings = documents.Where(d => d.Kind == JobDocumentKind.TemperaturePhoto && d.TemperatureCelsius is not null)
            .Select(d => new ProofReading(Local(d.CapturedAtUtc), d.TemperatureCelsius!.Value, JobTemperaturePolicy.IsOutOfRange(job, d.TemperatureCelsius.Value)))
            .ToList();

        var images = new List<ProofImage>();
        foreach (var document in documents.Where(d => Formats.Contains(d.ContentType)).Take(MaxImages))
        {
            await using var stream = await files.OpenReadAsync(document.StorageKey, ct);
            if (stream is null)
            {
                continue; // a missing file never blocks the rest of the evidence
            }

            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, ct);
            images.Add(new ProofImage(Title(document), Caption(document, Local(document.CapturedAtUtc)), buffer.ToArray()));
        }

        var details = profile is null
            ? null
            : string.Join(" · ", new[] { profile.LegalName, profile.Address, profile.Phone, profile.Email }.Where(s => !string.IsNullOrWhiteSpace(s)));
        var pack = new ProofPack(
            company, details, job.CustomerName ?? "Customer", job.Reference, job.Origin, job.Destination,
            string.Join(" · ", new[] { job.Vehicle.Name, job.Vehicle.Registration }.Where(s => !string.IsNullOrWhiteSpace(s))),
            job.Status switch { JobStatus.Delivered => "Delivered", JobStatus.InProgress => "On the way", JobStatus.Cancelled => "Cancelled", _ => "Planned" },
            job.RecipientName, job.DeliveryNote,
            job is { MinTempCelsius: { } min, MaxTempCelsius: { } max } ? $"{min:0.#} to {max:0.#} °C" : null,
            timeline, waits, readings, images, terms.Zone.Id, Local(nowUtc));

        var name = string.Concat((job.Reference ?? job.Id.ToString("N")[..8]).Where(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_'));
        return new PdfFile(renderer.Render(pack), $"proof-{(name.Length > 0 ? name : "delivery")}.pdf");
    }

    private static string Title(Domain.Jobs.JobDocument d) => d.Kind switch
    {
        JobDocumentKind.PickupDocument => "Loading note",
        JobDocumentKind.DeliveryPhoto => "Proof of delivery",
        JobDocumentKind.TemperaturePhoto => $"Temperature reading: {d.TemperatureCelsius:0.#} °C",
        JobDocumentKind.Signature => "Receiver's signature",
        JobDocumentKind.Exception => "Problem reported",
        _ => "Photo",
    };

    private static string Caption(Domain.Jobs.JobDocument d, DateTime local)
    {
        var where = d is { Latitude: { } lat, Longitude: { } lon } ? $" · {lat:0.00000}, {lon:0.00000}" : string.Empty;
        var note = string.IsNullOrWhiteSpace(d.Note) ? string.Empty : $" · {d.Note}";
        return $"{local:dd MMM yyyy, HH:mm}{where}{note}";
    }

    private static string Evidence(StopWait w)
    {
        if (w.Source == ArrivalSource.Gps)
        {
            return "Arrival recorded by the phone's GPS at the address";
        }

        if (w.DistanceFromStopMeters is not { } meters)
        {
            return "Driver confirmed arrival (no address location to compare)";
        }

        var distance = meters >= 1000 ? $"{meters / 1000:0.0} km" : $"{meters:0} m";
        return w.Verified
            ? $"Driver confirmed arrival {distance} from the address"
            : $"Driver confirmed arrival {distance} from the address (too far to count as proof)";
    }
}

/// <summary>The proof pack for one of the company's jobs.</summary>
[RequiresPolicy(Policies.Office)]
public sealed record GetJobProofPackQuery(Guid JobId) : IQuery<PdfFile>;

public sealed class GetJobProofPackHandler(
    IAppDbContext db, ITenantContext tenant, IFileStore files, IProofPackRenderer renderer, TimeProvider clock)
    : IQueryHandler<GetJobProofPackQuery, PdfFile>
{
    public async ValueTask<PdfFile> Handle(GetJobProofPackQuery query, CancellationToken cancellationToken)
    {
        var job = await db.VisibleJobAsync(tenant, query.JobId, cancellationToken);
        return await ProofPacks.RenderAsync(db, files, renderer, job, clock.GetUtcNow().UtcDateTime, cancellationToken);
    }
}
