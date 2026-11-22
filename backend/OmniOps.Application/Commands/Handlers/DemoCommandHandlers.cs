using MediatR;
using OmniOps.Application.Dtos;
using OmniOps.Core.Domain;
using OmniOps.Core.Entities;
using OmniOps.Core.Enums;
using OmniOps.Core.Interfaces;

namespace OmniOps.Application.Commands.Handlers;

/// <summary>
/// Loads a realistic delivery day for a first look at the product: two trucks on the road with a GPS trail
/// and one delivery waiting to start. Running it again moves the trucks to where they would be now
/// (at an average truck speed along their route) instead of creating duplicates.
/// </summary>
public class BootstrapDemoCommandHandler : IRequestHandler<BootstrapDemoCommand, DemoBootstrapDto>
{
    public const string ReferencePrefix = "DEMO-";
    private static readonly TimeSpan TrailInterval = TimeSpan.FromMinutes(5);

    internal sealed record DemoRoute(
        string Reference, string VehicleLabel, string Customer, string Destination,
        TimeSpan StartedAgo, (double Lat, double Lon)[] Path, (double Min, double Max)? ChilledRange = null);

    private static readonly (double, double) SkopjeDepot = (41.9981, 21.4254);

    internal static readonly DemoRoute[] Routes =
    [
        new($"{ReferencePrefix}1042", "Truck-001", "Vero Market Bitola", "Bitola, North Macedonia", TimeSpan.FromMinutes(70),
            [SkopjeDepot, (41.94, 21.53), (41.715, 21.775), (41.55, 21.70), (41.345, 21.555), (41.20, 21.45), (41.0316, 21.3302)],
            ChilledRange: (2, 8)),
        new($"{ReferencePrefix}1043", "Truck-002", "Tinex Kumanovo", "Kumanovo, North Macedonia", TimeSpan.FromMinutes(15),
            [SkopjeDepot, (42.03, 21.55), (42.08, 21.65), (42.1322, 21.7144)]),
        new($"{ReferencePrefix}1044", "Truck-003", "Ramstore Tetovo", "Tetovo, North Macedonia", TimeSpan.Zero,
            [SkopjeDepot, (42.0106, 20.9715)]),
    ];

    private readonly IJobRepository _jobs;
    private readonly ITenantContext _tenant;
    private readonly IMediator _mediator;
    private readonly TimeProvider _clock;

    public BootstrapDemoCommandHandler(IJobRepository jobs, ITenantContext tenant, IMediator mediator, TimeProvider clock)
    {
        _jobs = jobs;
        _tenant = tenant;
        _mediator = mediator;
        _clock = clock;
    }

    public async Task<DemoBootstrapDto> Handle(BootstrapDemoCommand request, CancellationToken cancellationToken)
    {
        var organizationId = _tenant.OrganizationId;
        var now = _clock.GetUtcNow().UtcDateTime;
        var vehicles = (await _jobs.ListVehiclesAsync(organizationId, cancellationToken)).ToDictionary(v => v.ExternalId);
        var driver = (await _jobs.ListDriversAsync(organizationId, cancellationToken)).FirstOrDefault();
        var open = await _jobs.ListAsync(
            organizationId, [TripStatus.Planned, TripStatus.InProgress], null, null, 200, cancellationToken);

        int created = 0, points = 0, onTheWay = 0, planned = 0;
        foreach (var route in Routes)
        {
            if (!vehicles.TryGetValue(route.VehicleLabel, out var vehicle))
            {
                continue;
            }

            var job = open.FirstOrDefault(j => j.Reference == route.Reference);
            if (job is null)
            {
                job = NewJob(route, vehicle, driver, organizationId, now);
                await _jobs.AddAsync(job, cancellationToken);
                await _jobs.SaveChangesAsync(cancellationToken);
                created++;
            }

            if (job.Status == TripStatus.Planned)
            {
                planned++;
                continue;
            }

            // A fresh job gets its whole trail so far; a re-run just adds where the truck is now.
            // The API takes points from the last 7 days only; a long-forgotten demo just resumes from 6 days ago.
            var since = job.LastPositionAtUtc ?? job.StartedAtUtc ?? now;
            if (since < now.AddDays(-6)) since = now.AddDays(-6);
            var trail = TrailTimes(since, now)
                .Select(at => ToPoint(route, (at - (job.StartedAtUtc ?? now)).TotalHours * DeliveryEta.AverageTruckSpeedKmh, at))
                .ToList();
            if (trail.Count > 0)
            {
                var result = await _mediator.Send(new RecordJobLocationsCommand(job.Id, trail), cancellationToken);
                points += result.Accepted;
            }

            onTheWay++;
        }

        var message = created > 0
            ? $"Loaded a demo day: {onTheWay} truck(s) on the way and {planned} delivery waiting to start."
            : $"Demo trucks moved to their current positions ({onTheWay} on the way).";
        return new DemoBootstrapDto(created, onTheWay, points, message);
    }

    private static Trip NewJob(DemoRoute route, Vehicle vehicle, Driver? driver, Guid organizationId, DateTime now)
    {
        var end = route.Path[^1];
        var running = route.StartedAgo > TimeSpan.Zero;
        return new Trip
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            FleetId = vehicle.FleetId,
            VehicleId = vehicle.Id,
            DriverId = driver?.Id,
            Status = running ? TripStatus.InProgress : TripStatus.Planned,
            CreatedAtUtc = now - route.StartedAgo - TimeSpan.FromMinutes(30),
            StartedAtUtc = running ? now - route.StartedAgo : null,
            CustomerName = route.Customer,
            Reference = route.Reference,
            Origin = "Skopje depot",
            Destination = route.Destination,
            DestinationLatitude = end.Lat,
            DestinationLongitude = end.Lon,
            PlannedDeliveryUtc = running ? null : now.AddHours(3),
            MinTempCelsius = route.ChilledRange?.Min,
            MaxTempCelsius = route.ChilledRange?.Max,
            TrackingToken = TrackingToken.Create(),
        };
    }

    /// <summary>Times from <paramref name="from"/> (exclusive) to <paramref name="to"/> (inclusive) every 5 minutes, ending at <paramref name="to"/>.</summary>
    private static IEnumerable<DateTime> TrailTimes(DateTime from, DateTime to)
    {
        for (var t = from + TrailInterval; t < to; t += TrailInterval)
        {
            yield return t;
        }

        if (to > from)
        {
            yield return to;
        }
    }

    private static JobLocationPoint ToPoint(DemoRoute route, double travelledKm, DateTime at)
    {
        var (lat, lon) = PositionAlong(route.Path, travelledKm);
        var arrived = travelledKm >= RouteLengthKm(route.Path);
        return new JobLocationPoint(Guid.NewGuid(), lat, lon, arrived ? 0 : DeliveryEta.AverageTruckSpeedKmh, null, 10, at);
    }

    internal static double RouteLengthKm((double Lat, double Lon)[] path) =>
        path.Zip(path.Skip(1)).Sum(p => DeliveryEta.DistanceKm(p.First.Lat, p.First.Lon, p.Second.Lat, p.Second.Lon));

    /// <summary>Point reached after <paramref name="km"/> along the path; stays at the end once there.</summary>
    internal static (double Lat, double Lon) PositionAlong((double Lat, double Lon)[] path, double km)
    {
        var left = Math.Max(0, km);
        for (var i = 0; i < path.Length - 1; i++)
        {
            var (a, b) = (path[i], path[i + 1]);
            var leg = DeliveryEta.DistanceKm(a.Lat, a.Lon, b.Lat, b.Lon);
            if (left <= leg)
            {
                var f = leg == 0 ? 0 : left / leg;
                return (Math.Round(a.Lat + (b.Lat - a.Lat) * f, 5), Math.Round(a.Lon + (b.Lon - a.Lon) * f, 5));
            }

            left -= leg;
        }

        return path[^1];
    }
}
