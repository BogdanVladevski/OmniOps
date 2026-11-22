using MediatR;
using OmniOps.Application.Abstractions;
using OmniOps.Application.Dtos;
using OmniOps.Core.Authorization;

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
    DateTime? PlannedDeliveryUtc) : IRequest<JobDto>, IAuthorizedRequest
{
    public string RequiredPolicy => AuthorizationPolicies.VehicleRead;
}

/// <summary>Driver starts the job. Repeating it is harmless (phones retry).</summary>
public record StartJobCommand(Guid JobId) : IRequest<JobDto>, IAuthorizedRequest
{
    public string RequiredPolicy => AuthorizationPolicies.VehicleRead;
}

public record JobLocationPoint(
    Guid Id,
    double Latitude,
    double Longitude,
    double? SpeedKmh,
    double? Heading,
    double? AccuracyMeters,
    DateTime RecordedAtUtc);

/// <summary>
/// GPS points from the driver's phone. Processed synchronously so the phone keeps (and resends) anything
/// not acknowledged; points already stored are skipped, so resending is always safe.
/// </summary>
public record RecordJobLocationsCommand(Guid JobId, IReadOnlyList<JobLocationPoint> Points)
    : IRequest<JobLocationsResultDto>, IAuthorizedRequest
{
    public string RequiredPolicy => AuthorizationPolicies.VehicleRead;
}

public record AddJobPhotoCommand(
    Guid JobId,
    Stream Content,
    string ContentType,
    long SizeBytes,
    double? Latitude,
    double? Longitude) : IRequest<JobDocumentDto>, IAuthorizedRequest
{
    public const long MaxBytes = 10 * 1024 * 1024;
    public const int MaxPhotosPerJob = 10;
    public static readonly IReadOnlyDictionary<string, string> AllowedTypes = new Dictionary<string, string>
    {
        ["image/jpeg"] = "jpg",
        ["image/png"] = "png",
        ["image/webp"] = "webp",
        ["image/heic"] = "heic",
    };

    public string RequiredPolicy => AuthorizationPolicies.VehicleRead;
}

/// <summary>Closes the job with proof of delivery. Needs at least one delivery photo. Repeating it is harmless.</summary>
public record DeliverJobCommand(Guid JobId, string RecipientName, string? Note, double? Latitude, double? Longitude)
    : IRequest<JobDto>, IAuthorizedRequest
{
    public string RequiredPolicy => AuthorizationPolicies.VehicleRead;
}
