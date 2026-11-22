using MediatR;
using OmniOps.Application.Abstractions;
using OmniOps.Application.Dtos;
using OmniOps.Core.Authorization;
using OmniOps.Core.Enums;

namespace OmniOps.Application.Commands;

public record CreateJobCommand(
    Guid VehicleId,
    Guid? DriverId,
    string CustomerName,
    string? Reference,
    string? Origin,
    string Destination,
    double? DestinationLatitude,
    double? DestinationLongitude,
    DateTimeOffset? PlannedDeliveryUtc,
    double? MinTempCelsius = null,
    double? MaxTempCelsius = null,
    double? OriginLatitude = null,
    double? OriginLongitude = null) : IRequest<JobDto>, IAuthorizedRequest
{
    public string RequiredPolicy => AuthorizationPolicies.VehicleRead;
}

/// <summary>
/// Driver: "I've arrived" at the pickup (before starting the trip) or the delivery. Starts the waiting clock with
/// the phone's position as evidence. GPS also detects delivery arrivals by itself; the earlier record wins.
/// Repeating it is harmless.
/// </summary>
public record MarkArrivedCommand(Guid JobId, string Stop, DateTimeOffset? ArrivedAt, double? Latitude, double? Longitude)
    : IRequest<JobDto>, IAuthorizedRequest
{
    public string RequiredPolicy => AuthorizationPolicies.JobWork;
}

/// <summary>System: tells the office when a truck's free waiting time has run out (once per stop).</summary>
public record ProcessWaitingAlertsCommand : IRequest<int>;

/// <summary>
/// Driver starts the job. Repeating it is harmless (phones retry). <paramref name="StartedAt"/> is when the driver
/// tapped Start on the phone, which may have been offline.
/// </summary>
public record StartJobCommand(Guid JobId, DateTimeOffset? StartedAt = null) : IRequest<JobDto>, IAuthorizedRequest
{
    public string RequiredPolicy => AuthorizationPolicies.JobWork;
}

public record JobLocationPoint(
    Guid Id,
    double Latitude,
    double Longitude,
    double? SpeedKmh,
    double? Heading,
    double? AccuracyMeters,
    // Offset-aware: phones may send local time with an offset ("+02:00"); a DateTime would come out as
    // Kind=Local, which PostgreSQL rejects and which shifts by the server's time zone.
    DateTimeOffset RecordedAtUtc);

/// <summary>
/// GPS points from the driver's phone. Processed synchronously so the phone keeps (and resends) anything
/// not acknowledged; points already stored are skipped, so resending is always safe.
/// </summary>
public record RecordJobLocationsCommand(Guid JobId, IReadOnlyList<JobLocationPoint> Points)
    : IRequest<JobLocationsResultDto>, IAuthorizedRequest
{
    public string RequiredPolicy => AuthorizationPolicies.JobWork;
}

/// <summary>
/// A photo attached to a job: proof of delivery, the signed loading note, or the fridge display with the
/// reading the driver entered (<paramref name="TemperatureCelsius"/>, required for temperature photos only).
/// </summary>
public record AddJobDocumentCommand(
    Guid JobId,
    TripDocumentKind Kind,
    Stream Content,
    string ContentType,
    long SizeBytes,
    double? Latitude,
    double? Longitude,
    double? TemperatureCelsius = null,
    string? Note = null,
    Guid? ClientId = null,
    DateTimeOffset? CapturedAt = null) : IRequest<JobDocumentDto>, IAuthorizedRequest
{
    public const long MaxBytes = 10 * 1024 * 1024;
    public const int MaxPhotosPerKind = 10;
    public const int MaxSignatures = 3;
    public const int MaxNoteLength = 500;
    public const int MaxTemperatureReadings = 50;
    public static readonly IReadOnlyDictionary<string, string> AllowedTypes = new Dictionary<string, string>
    {
        ["image/jpeg"] = "jpg",
        ["image/png"] = "png",
        ["image/webp"] = "webp",
        ["image/heic"] = "heic",
    };

    public string RequiredPolicy => AuthorizationPolicies.JobWork;
}

/// <summary>
/// Closes the job with proof of delivery: at least one delivery photo or the receiver's signature, and for
/// refrigerated jobs a temperature reading from the 30 minutes before hand-over. Repeating it is harmless.
/// <paramref name="DeliveredAt"/> is when the driver confirmed on the phone (possibly offline).
/// </summary>
public record DeliverJobCommand(
    Guid JobId, string RecipientName, string? Note, double? Latitude, double? Longitude, DateTimeOffset? DeliveredAt = null)
    : IRequest<JobDto>, IAuthorizedRequest
{
    public string RequiredPolicy => AuthorizationPolicies.JobWork;
}
