using OmniOps.Core.Domain;
using OmniOps.Core.Entities;
using OmniOps.Core.Enums;

namespace OmniOps.Application.Dtos;

public record JobDto(
    Guid Id,
    string Status,
    Guid VehicleId,
    string VehicleLabel,
    string? Registration,
    Guid? DriverId,
    string? DriverName,
    string? CustomerName,
    string? Reference,
    string? Origin,
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
    int PhotoCount,
    string? TrackingPath,
    double? RemainingKm,
    DateTime? EtaUtc,
    double? MinTempCelsius,
    double? MaxTempCelsius,
    double? LastTemperatureCelsius,
    DateTime? LastTemperatureAtUtc,
    int TemperatureReadingCount,
    int TemperatureOutOfRangeCount,
    DateTime? TemperatureCheckDueUtc,
    int PickupDocumentCount,
    Guid? InvoiceId,
    int SignatureCount,
    int ExceptionCount,
    double? OriginLatitude,
    double? OriginLongitude,
    DateTime? PickupArrivedAtUtc,
    DateTime? DeliveryArrivedAtUtc)
{
    public static JobDto FromEntity(Trip trip)
    {
        var (remainingKm, eta) = Estimate(trip);
        var readings = JobTemperaturePolicy.Readings(trip).ToList();
        var last = readings.LastOrDefault();
        return new(
        trip.Id,
        trip.Status.ToString(),
        trip.VehicleId,
        trip.Vehicle?.ExternalId ?? string.Empty,
        trip.Vehicle?.Registration,
        trip.DriverId,
        trip.Driver?.FullName,
        trip.CustomerName,
        trip.Reference,
        trip.Origin,
        trip.Destination,
        trip.DestinationLatitude,
        trip.DestinationLongitude,
        trip.PlannedDeliveryUtc,
        trip.CreatedAtUtc,
        trip.StartedAtUtc,
        trip.CompletedAtUtc,
        trip.RecipientName,
        trip.DeliveryNote,
        trip.LastLatitude,
        trip.LastLongitude,
        trip.LastSpeedKmh,
        trip.LastPositionAtUtc,
        trip.Documents.Count(d => d.Kind == TripDocumentKind.DeliveryPhoto),
        trip.TrackingToken is null ? null : $"/t/{trip.TrackingToken}",
        remainingKm,
        eta,
        trip.MinTempCelsius,
        trip.MaxTempCelsius,
        last?.TemperatureCelsius,
        last?.CapturedAtUtc,
        readings.Count,
        readings.Count(r => JobTemperaturePolicy.IsOutOfRange(trip, r.TemperatureCelsius!.Value)),
        JobTemperaturePolicy.NextCheckDueUtc(trip),
        trip.Documents.Count(d => d.Kind == TripDocumentKind.PickupDocument),
        trip.InvoiceId,
        trip.Documents.Count(d => d.Kind == TripDocumentKind.Signature),
        trip.Documents.Count(d => d.Kind == TripDocumentKind.Exception),
        trip.OriginLatitude,
        trip.OriginLongitude,
        trip.PickupArrivedAtUtc,
        trip.DeliveryArrivedAtUtc);
    }

    /// <summary>Arrival estimate while on the way and both positions are known; otherwise nothing.</summary>
    private static (double?, DateTime?) Estimate(Trip trip)
    {
        if (trip.Status != TripStatus.InProgress
            || trip.LastLatitude is not { } lat || trip.LastLongitude is not { } lon || trip.LastPositionAtUtc is not { } at
            || trip.DestinationLatitude is not { } dLat || trip.DestinationLongitude is not { } dLon)
        {
            return (null, null);
        }

        var (km, eta) = DeliveryEta.Estimate(lat, lon, dLat, dLon, at);
        return (km, eta);
    }
}

public record JobDocumentDto(
    Guid Id, string Kind, string ContentType, long SizeBytes, DateTime CapturedAtUtc,
    double? Latitude, double? Longitude, double? TemperatureCelsius, string? Note = null, Guid? ClientId = null)
{
    public static JobDocumentDto FromEntity(TripDocument d) =>
        new(d.Id, d.Kind.ToString(), d.ContentType, d.SizeBytes, d.CapturedAtUtc, d.Latitude, d.Longitude, d.TemperatureCelsius,
            d.Note, d.ClientId);
}

public record JobDetailDto(JobDto Job, IReadOnlyList<JobDocumentDto> Documents, IReadOnlyList<WaitDto>? Waits = null);

/// <summary>
/// Waiting at a stop: when the truck arrived and how (GPS or the driver's tap, and how far from the address),
/// how long it waited, and what is chargeable under the company's terms.
/// </summary>
public record WaitDto(
    string Stop,
    DateTime ArrivedAtUtc,
    DateTime? ReleasedAtUtc,
    string Source,
    double? DistanceFromStopMeters,
    bool Verified,
    bool Running,
    int WaitedMinutes,
    int FreeMinutes,
    DateTime ChargedFromUtc,
    int ChargedMinutes,
    decimal? Amount,
    decimal? RatePerHour,
    string Currency)
{
    public static WaitDto From(StopWait w, decimal? rate, string currency) =>
        new(w.Stop, w.ArrivedAtUtc, w.ReleasedAtUtc, w.Source.ToString(), w.DistanceFromStopMeters, w.Verified, w.Running,
            w.WaitedMinutes, w.FreeMinutes, w.ChargedFromUtc, w.ChargedMinutes, w.Amount, rate, currency);
}

public record AssignableOptionDto(Guid Id, string Label, string? Detail);

public record JobAssignablesDto(IReadOnlyList<AssignableOptionDto> Vehicles, IReadOnlyList<AssignableOptionDto> Drivers);

public record JobLocationsResultDto(int Accepted, int Skipped);

/// <summary>A file's bytes for download. The caller disposes <see cref="Content"/>.</summary>
public record JobDocumentContent(Stream Content, string ContentType);

/// <summary>What the public tracking page may show — nothing about the driver, vehicle or other jobs.</summary>
public record PublicTrackingDto(
    string CompanyName,
    string? Reference,
    string? CustomerName,
    string Status,
    string? Origin,
    string? Destination,
    double? DestinationLatitude,
    double? DestinationLongitude,
    DateTime? PlannedDeliveryUtc,
    double? Latitude,
    double? Longitude,
    DateTime? PositionAtUtc,
    double? RemainingKm,
    DateTime? EtaUtc,
    DateTime? DeliveredAtUtc,
    string? RecipientName,
    DateTime? ArrivedAtUtc = null,
    DateTime? WaitingChargedFromUtc = null);
