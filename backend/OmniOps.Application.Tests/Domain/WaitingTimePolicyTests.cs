using OmniOps.Core.Domain;
using OmniOps.Core.Entities;
using OmniOps.Core.Enums;

namespace OmniOps.Application.Tests.Domain;

public class WaitingTimePolicyTests
{
    // Bitola delivery address and points around it.
    private const double StopLat = 41.0297, StopLon = 21.3292;
    private static readonly DateTime T0 = new(2026, 11, 22, 9, 0, 0, DateTimeKind.Utc);

    private static (DateTime, double, double) At(int minutes, double metersNorth) =>
        (T0.AddMinutes(minutes), StopLat + metersNorth / 111_320.0, StopLon);

    [Fact]
    public void Arrival_IsTheFirstPointInsideTheStopArea()
    {
        var arrival = WaitingTimePolicy.TrackArrival(null, [At(0, 5000), At(10, 1200), At(14, 200), At(20, 50), At(60, 30)], StopLat, StopLon);
        Assert.Equal(T0.AddMinutes(14), arrival);
    }

    [Fact]
    public void DrivingThroughAndLeaving_IsNotAnArrival()
    {
        var arrival = WaitingTimePolicy.TrackArrival(null, [At(0, 200), At(5, 900), At(30, 3000)], StopLat, StopLon);
        Assert.Null(arrival);
    }

    [Fact]
    public void MovingAroundTheYard_DoesNotRestartTheClock()
    {
        // 400 m: past the gate area but not "left" — the truck is repositioning, not leaving.
        var arrival = WaitingTimePolicy.TrackArrival(null, [At(0, 100), At(30, 400), At(40, 100)], StopLat, StopLon);
        Assert.Equal(T0, arrival);
    }

    [Fact]
    public void ComingBackLater_StartsANewArrival()
    {
        var arrival = WaitingTimePolicy.TrackArrival(null, [At(0, 100), At(10, 2000), At(70, 150)], StopLat, StopLon);
        Assert.Equal(T0.AddMinutes(70), arrival);
    }

    [Fact]
    public void ResentAndLatePoints_AreHarmless()
    {
        var first = WaitingTimePolicy.TrackArrival(null, [At(20, 100), At(25, 90)], StopLat, StopLon);
        var again = WaitingTimePolicy.TrackArrival(first, [At(20, 100), At(25, 90)], StopLat, StopLon);
        var earlierPointArrivesLate = WaitingTimePolicy.TrackArrival(again, [At(15, 200)], StopLat, StopLon);
        Assert.Equal(T0.AddMinutes(20), again);
        Assert.Equal(T0.AddMinutes(15), earlierPointArrivesLate);
    }

    [Theory]
    [InlineData(90, 120, 0)]
    [InlineData(120, 120, 0)]
    [InlineData(121, 120, 15)] // every started quarter hour
    [InlineData(135, 120, 15)]
    [InlineData(136, 120, 30)]
    [InlineData(250, 120, 135)]
    public void Charged_IsTimePastTheFreeTime_InStartedQuarterHours(int waited, int free, int charged) =>
        Assert.Equal(charged, WaitingTimePolicy.ChargedMinutes(waited, free));

    [Fact]
    public void Amount_UsesTheHourlyRate_AndNothingWithoutOne()
    {
        Assert.Equal(22.50m, WaitingTimePolicy.Amount(45, 30m));
        Assert.Null(WaitingTimePolicy.Amount(45, null));
        Assert.Null(WaitingTimePolicy.Amount(0, 30m));
    }

    [Fact]
    public void Waits_MeasurePickupUntilStart_AndDeliveryUntilHandOver()
    {
        var job = new Trip
        {
            Status = TripStatus.Completed,
            OriginLatitude = 41.99, OriginLongitude = 21.43,
            DestinationLatitude = StopLat, DestinationLongitude = StopLon,
            PickupArrivedAtUtc = T0, PickupArrivalSource = ArrivalSource.Driver,
            PickupArrivalLatitude = 41.9905, PickupArrivalLongitude = 21.43, // ~55 m from the pickup
            StartedAtUtc = T0.AddMinutes(150),
            DeliveryArrivedAtUtc = T0.AddHours(5), DeliveryArrivalSource = ArrivalSource.Gps,
            CompletedAtUtc = T0.AddHours(5).AddMinutes(40),
        };

        var waits = WaitingTimePolicy.Waits(job, freeMinutes: 120, ratePerHour: 30m, nowUtc: T0.AddDays(1));

        var pickup = Assert.Single(waits, w => w.Stop == WaitingTimePolicy.Pickup);
        Assert.Equal(150, pickup.WaitedMinutes);
        Assert.Equal(30, pickup.ChargedMinutes);
        Assert.Equal(15m, pickup.Amount);
        Assert.True(pickup.Verified);
        Assert.False(pickup.Running);

        var delivery = Assert.Single(waits, w => w.Stop == WaitingTimePolicy.Delivery);
        Assert.Equal(40, delivery.WaitedMinutes);
        Assert.Equal(0, delivery.ChargedMinutes);
        Assert.Null(delivery.Amount);
    }

    [Fact]
    public void ATapFarFromTheAddress_IsShownButNotTrusted()
    {
        var job = new Trip
        {
            Status = TripStatus.Planned, OriginLatitude = 41.99, OriginLongitude = 21.43,
            PickupArrivedAtUtc = T0, PickupArrivalSource = ArrivalSource.Driver,
            PickupArrivalLatitude = 42.05, PickupArrivalLongitude = 21.43, // ~6.7 km away
        };
        var wait = Assert.Single(WaitingTimePolicy.Waits(job, 120, 30m, T0.AddMinutes(30)));
        Assert.False(wait.Verified);
        Assert.True(wait.Running);
        Assert.InRange(wait.DistanceFromStopMeters!.Value, 6000, 7500);
    }

    [Fact]
    public void ACancelledJob_StopsCounting()
    {
        var job = new Trip { Status = TripStatus.Cancelled, DeliveryArrivedAtUtc = T0 };
        Assert.Equal(0, Assert.Single(WaitingTimePolicy.Waits(job, 120, 30m, T0.AddHours(10))).WaitedMinutes);
    }
}
