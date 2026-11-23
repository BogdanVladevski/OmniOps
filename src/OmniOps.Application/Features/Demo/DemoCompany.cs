using Mediator;
using Microsoft.EntityFrameworkCore;
using OmniOps.Application.Common.Interfaces;
using OmniOps.Application.Common.Security;
using OmniOps.Application.Features.Jobs;
using OmniOps.Domain.Accounts;
using OmniOps.Domain.Billing;
using OmniOps.Domain.Common;
using OmniOps.Domain.Fleet;
using OmniOps.Domain.Jobs;

namespace OmniOps.Application.Features.Demo;

/// <summary>A sample company to try the product with. Only offered on development servers (the API decides).</summary>
public static class DemoCompany
{
    public const string Slug = "omniops-demo";
    public const string OwnerEmail = "demo@omniops.local";
    public const string ReferencePrefix = "DEMO-";

    internal static readonly (string Name, string Registration)[] Trucks =
        [("Truck-001", "SK-1001-AB"), ("Truck-002", "SK-2002-AB"), ("Truck-003", "SK-3003-AB")];

    internal static readonly (string Name, string Phone)[] Drivers =
        [("Marko Petrovski", "+389 70 111 222"), ("Ana Ristova", "+389 71 333 444")];
}

/// <summary>Creates the demo company (owner, trucks, drivers, invoice details) if it doesn't exist. Returns the owner's id.</summary>
[Anonymous]
public sealed record EnsureDemoCompanyCommand : ICommand<Guid>;

public sealed class EnsureDemoCompanyHandler(IAppDbContext db, TimeProvider clock) : ICommandHandler<EnsureDemoCompanyCommand, Guid>
{
    public async ValueTask<Guid> Handle(EnsureDemoCompanyCommand command, CancellationToken cancellationToken)
    {
        var owner = await db.Users.AcrossCompanies().FirstOrDefaultAsync(u => u.Email == DemoCompany.OwnerEmail, cancellationToken);
        if (owner is not null)
        {
            return owner.Id;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var company = new Organization { Id = Guid.NewGuid(), Name = "Vardar Transport (demo)", Slug = DemoCompany.Slug, CreatedAtUtc = now };
        // No password: the demo owner can only be entered through the development-only demo sign-in.
        owner = new UserAccount
        {
            Id = Guid.NewGuid(), OrganizationId = company.Id, Email = DemoCompany.OwnerEmail, DisplayName = "Demo owner",
            Role = UserRole.Owner, CreatedAtUtc = now,
        };
        db.Organizations.Add(company);
        db.Users.Add(owner);
        db.Vehicles.AddRange(DemoCompany.Trucks.Select(t => new Vehicle
        {
            Id = Guid.NewGuid(), OrganizationId = company.Id, Name = t.Name, Registration = t.Registration, CreatedAtUtc = now,
        }));
        db.Drivers.AddRange(DemoCompany.Drivers.Select(d => new Driver
        {
            Id = Guid.NewGuid(), OrganizationId = company.Id, FullName = d.Name, Phone = d.Phone, CreatedAtUtc = now,
        }));
        db.BillingProfiles.Add(new BillingProfile
        {
            OrganizationId = company.Id, LegalName = "Vardar Transport DOOEL", Address = "Partizanska 1, 1000 Skopje, North Macedonia",
            TaxId = "MK4030000000000", Iban = "MK07250120000058984", BankName = "Demo Bank", Currency = "EUR", DefaultVatRatePercent = 18,
            InvoicePrefix = "VT", WaitingRatePerHour = 30, TimeZone = "Europe/Skopje", UpdatedAtUtc = now,
        });
        await db.SaveChangesAsync(cancellationToken);
        return owner.Id;
    }
}

public sealed record DemoStatusDto(bool IsDemoCompany);

[RequiresPolicy(Policies.Office)]
public sealed record GetDemoStatusQuery : IQuery<DemoStatusDto>;

public sealed class GetDemoStatusHandler(IAppDbContext db, ITenantContext tenant) : IQueryHandler<GetDemoStatusQuery, DemoStatusDto>
{
    public async ValueTask<DemoStatusDto> Handle(GetDemoStatusQuery query, CancellationToken cancellationToken) =>
        new(await db.Organizations.AnyAsync(o => o.Id == tenant.OrganizationId && o.Slug == DemoCompany.Slug, cancellationToken));
}

public sealed record DemoDayDto(int JobsCreated, int TrucksOnTheWay, int LocationPoints, string Message);

/// <summary>
/// Loads a realistic delivery day in the demo company: trucks on the road with a GPS trail and a delivery waiting to
/// start. Running it again moves the trucks to where they would be now instead of creating duplicates.
/// </summary>
[RequiresPolicy(Policies.Office)]
public sealed record LoadDemoDayCommand : ICommand<DemoDayDto>;

public sealed class LoadDemoDayHandler(IAppDbContext db, ITenantContext tenant, IMediator mediator, TimeProvider clock)
    : ICommandHandler<LoadDemoDayCommand, DemoDayDto>
{
    private static readonly TimeSpan TrailInterval = TimeSpan.FromMinutes(5);
    private static readonly (double, double) SkopjeDepot = (41.9981, 21.4254);

    internal sealed record DemoRoute(
        string Reference, string Truck, string Customer, string Destination,
        TimeSpan StartedAgo, (double Lat, double Lon)[] Path, (double Min, double Max)? ChilledRange = null);

    internal static readonly DemoRoute[] Routes =
    [
        new($"{DemoCompany.ReferencePrefix}1042", "Truck-001", "Vero Market Bitola", "Bitola, North Macedonia", TimeSpan.FromMinutes(70),
            [SkopjeDepot, (41.94, 21.53), (41.715, 21.775), (41.55, 21.70), (41.345, 21.555), (41.20, 21.45), (41.0316, 21.3302)],
            ChilledRange: (2, 8)),
        new($"{DemoCompany.ReferencePrefix}1043", "Truck-002", "Tinex Kumanovo", "Kumanovo, North Macedonia", TimeSpan.FromMinutes(15),
            [SkopjeDepot, (42.03, 21.55), (42.08, 21.65), (42.1322, 21.7144)]),
        new($"{DemoCompany.ReferencePrefix}1044", "Truck-003", "Ramstore Tetovo", "Tetovo, North Macedonia", TimeSpan.Zero,
            [SkopjeDepot, (42.0106, 20.9715)]),
    ];

    public async ValueTask<DemoDayDto> Handle(LoadDemoDayCommand command, CancellationToken cancellationToken)
    {
        if (!(await mediator.Send(new GetDemoStatusQuery(), cancellationToken)).IsDemoCompany)
        {
            throw new BusinessRuleException("Demo data can only be loaded into the demo company.");
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var trucks = await db.Vehicles.ToDictionaryAsync(v => v.Name, cancellationToken);
        var driver = await db.Drivers.OrderBy(d => d.FullName).FirstOrDefaultAsync(cancellationToken);
        var open = await db.Jobs.Where(j => j.Status == JobStatus.Planned || j.Status == JobStatus.InProgress).ToListAsync(cancellationToken);

        int created = 0, points = 0, onTheWay = 0, planned = 0;
        foreach (var route in Routes)
        {
            if (!trucks.TryGetValue(route.Truck, out var truck))
            {
                continue;
            }

            var job = open.FirstOrDefault(j => j.Reference == route.Reference);
            if (job is null)
            {
                job = NewJob(route, truck, driver, now);
                db.Jobs.Add(job);
                await db.SaveChangesAsync(cancellationToken);
                created++;
            }

            if (job.Status == JobStatus.Planned)
            {
                planned++;
                continue;
            }

            // A fresh job gets its whole trail so far; a re-run adds where the truck is now. Readings older than
            // the API accepts (7 days) are skipped: a long-forgotten demo resumes from 6 days ago.
            var since = job.LastPositionAtUtc ?? job.StartedAtUtc ?? now;
            if (since < now.AddDays(-6))
            {
                since = now.AddDays(-6);
            }

            var trail = TrailTimes(since, now)
                .Select(at => ToPoint(route, (at - (job.StartedAtUtc ?? now)).TotalHours * DeliveryEta.AverageTruckSpeedKmh, at))
                .ToList();
            if (trail.Count > 0)
            {
                points += (await mediator.Send(new RecordLocationsCommand(job.Id, trail), cancellationToken)).Accepted;
            }

            onTheWay++;
        }

        var message = created > 0
            ? $"Loaded a demo day: {onTheWay} truck(s) on the way and {planned} delivery waiting to start."
            : $"Demo trucks moved to their current positions ({onTheWay} on the way).";
        return new DemoDayDto(created, onTheWay, points, message);
    }

    private Job NewJob(DemoRoute route, Vehicle truck, Driver? driver, DateTime now)
    {
        var end = route.Path[^1];
        var running = route.StartedAgo > TimeSpan.Zero;
        return new Job
        {
            Id = Guid.NewGuid(),
            OrganizationId = tenant.OrganizationId,
            VehicleId = truck.Id,
            DriverId = driver?.Id,
            Status = running ? JobStatus.InProgress : JobStatus.Planned,
            CreatedAtUtc = now - route.StartedAgo - TimeSpan.FromMinutes(30),
            StartedAtUtc = running ? now - route.StartedAgo : null,
            CustomerName = route.Customer,
            Reference = route.Reference,
            Origin = "Skopje depot",
            OriginLatitude = SkopjeDepot.Item1,
            OriginLongitude = SkopjeDepot.Item2,
            Destination = route.Destination,
            DestinationLatitude = end.Lat,
            DestinationLongitude = end.Lon,
            PlannedDeliveryUtc = running ? null : now.AddHours(3),
            MinTempCelsius = route.ChilledRange?.Min,
            MaxTempCelsius = route.ChilledRange?.Max,
            TrackingToken = PublicToken.Create(),
        };
    }

    /// <summary>Every 5 minutes after <paramref name="from"/>, ending exactly at <paramref name="to"/>.</summary>
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

    private static LocationPoint ToPoint(DemoRoute route, double travelledKm, DateTime at)
    {
        var (lat, lon) = PositionAlong(route.Path, travelledKm);
        var arrived = travelledKm >= RouteLengthKm(route.Path);
        return new LocationPoint(Guid.NewGuid(), lat, lon, arrived ? 0 : DeliveryEta.AverageTruckSpeedKmh, null, 10, at);
    }

    internal static double RouteLengthKm((double Lat, double Lon)[] path) =>
        path.Zip(path.Skip(1)).Sum(p => DeliveryEta.DistanceKm(p.First.Lat, p.First.Lon, p.Second.Lat, p.Second.Lon));

    /// <summary>The point reached after <paramref name="km"/> along the path; stays at the end once there.</summary>
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
                return (Math.Round(a.Lat + ((b.Lat - a.Lat) * f), 5), Math.Round(a.Lon + ((b.Lon - a.Lon) * f), 5));
            }

            left -= leg;
        }

        return path[^1];
    }
}
