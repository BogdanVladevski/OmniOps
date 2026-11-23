using Microsoft.EntityFrameworkCore;
using OmniOps.Application.Common.Interfaces;
using OmniOps.Domain.Jobs;

namespace OmniOps.Application.Features.Jobs;

public sealed record JobDto(
    Guid Id,
    string Status,
    Guid VehicleId,
    string VehicleName,
    string? Registration,
    Guid? DriverId,
    string? DriverName,
    string? DriverPhone,
    string? CustomerName,
    string? Reference,
    string? Origin,
    double? OriginLatitude,
    double? OriginLongitude,
    string? Destination,
    double? DestinationLatitude,
    double? DestinationLongitude,
    DateTime? PlannedDeliveryUtc,
    DateTime CreatedAtUtc,
    DateTime? StartedAtUtc,
    DateTime? DeliveredAtUtc,
    string? RecipientName,
    string? DeliveryNote,
    double? LastLatitude,
    double? LastLongitude,
    double? LastSpeedKmh,
    DateTime? LastPositionAtUtc,
    double? RemainingKm,
    DateTime? EtaUtc,
    string? TrackingPath,
    double? MinTempCelsius,
    double? MaxTempCelsius,
    double? LastTemperatureCelsius,
    DateTime? LastTemperatureAtUtc,
    int TemperatureReadingCount,
    int TemperatureOutOfRangeCount,
    DateTime? TemperatureCheckDueUtc,
    int PhotoCount,
    int PickupDocumentCount,
    int SignatureCount,
    int ExceptionCount,
    DateTime? PickupArrivedAtUtc,
    DateTime? DeliveryArrivedAtUtc,
    Guid? InvoiceId,
    Guid? CustomerId)
{
    public static JobDto From(Job job)
    {
        var readings = JobTemperaturePolicy.Readings(job).ToList();
        var last = readings.LastOrDefault();
        var (remainingKm, eta) = Estimate(job);
        int Count(JobDocumentKind kind) => job.Documents.Count(d => d.Kind == kind);
        return new(
            job.Id, job.Status.ToString(), job.VehicleId, job.Vehicle?.Name ?? string.Empty, job.Vehicle?.Registration,
            job.DriverId, job.Driver?.FullName, job.Driver?.Phone,
            job.CustomerName, job.Reference,
            job.Origin, job.OriginLatitude, job.OriginLongitude, job.Destination, job.DestinationLatitude, job.DestinationLongitude,
            job.PlannedDeliveryUtc, job.CreatedAtUtc, job.StartedAtUtc, job.DeliveredAtUtc, job.RecipientName, job.DeliveryNote,
            job.LastLatitude, job.LastLongitude, job.LastSpeedKmh, job.LastPositionAtUtc, remainingKm, eta,
            job.TrackingToken is null ? null : $"/t/{job.TrackingToken}",
            job.MinTempCelsius, job.MaxTempCelsius, last?.TemperatureCelsius, last?.CapturedAtUtc, readings.Count,
            readings.Count(r => JobTemperaturePolicy.IsOutOfRange(job, r.TemperatureCelsius!.Value)),
            JobTemperaturePolicy.NextCheckDueUtc(job),
            Count(JobDocumentKind.DeliveryPhoto), Count(JobDocumentKind.PickupDocument), Count(JobDocumentKind.Signature),
            Count(JobDocumentKind.Exception), job.PickupArrivedAtUtc, job.DeliveryArrivedAtUtc, job.InvoiceId, job.CustomerId);
    }

    /// <summary>Arrival estimate while on the way and both positions are known; otherwise nothing.</summary>
    private static (double?, DateTime?) Estimate(Job job)
    {
        if (job.Status != JobStatus.InProgress
            || job.LastLatitude is not { } lat || job.LastLongitude is not { } lon || job.LastPositionAtUtc is not { } at
            || job.DestinationLatitude is not { } dLat || job.DestinationLongitude is not { } dLon)
        {
            return (null, null);
        }

        var (km, eta) = DeliveryEta.Estimate(lat, lon, dLat, dLon, at);
        return (km, eta);
    }
}

public sealed record JobDocumentDto(
    Guid Id, string Kind, string ContentType, long SizeBytes, DateTime CapturedAtUtc,
    double? Latitude, double? Longitude, double? TemperatureCelsius, string? Note, Guid? ClientId)
{
    public static JobDocumentDto From(JobDocument d) =>
        new(d.Id, d.Kind.ToString(), d.ContentType, d.SizeBytes, d.CapturedAtUtc, d.Latitude, d.Longitude, d.TemperatureCelsius, d.Note, d.ClientId);
}

/// <summary>A file's bytes for download. The caller disposes <see cref="Content"/>.</summary>
public sealed record FileContent(Stream Content, string ContentType);

/// <summary>Kinds of live alert the office app shows for jobs.</summary>
public static class JobAlerts
{
    public const string Delivered = "JobDelivered";
    public const string ProblemReported = "JobProblemReported";
    public const string TemperatureOutOfRange = "JobTemperatureOutOfRange";
    public const string WaitingCharged = "JobWaitingCharged";
}

internal static class JobQueries
{
    /// <summary>Jobs with what <see cref="JobDto"/> needs. Drivers only ever see their own.</summary>
    public static IQueryable<Job> Visible(this IAppDbContext db, ITenantContext tenant) =>
        db.Jobs
            .Include(j => j.Vehicle)
            .Include(j => j.Driver)
            .Include(j => j.Documents)
            .Where(j => tenant.DriverId == null || j.DriverId == tenant.DriverId);

    public static async Task<Job> VisibleJobAsync(this IAppDbContext db, ITenantContext tenant, Guid jobId, CancellationToken ct) =>
        await db.Visible(tenant).FirstOrDefaultAsync(j => j.Id == jobId, ct)
        ?? throw new KeyNotFoundException("Job not found.");
}
