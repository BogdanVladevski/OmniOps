namespace OmniOps.UnitTests.Domain;

public class CustomerUpdatesTests
{
    private static readonly DateTime Now = new(2026, 11, 23, 12, 0, 0, DateTimeKind.Utc);

    // Positions are placed due north of the destination; one degree of latitude is about 111 km.
    private const double DestinationLat = 41.715, DestinationLon = 21.775;

    private static Job OnTheWay(double kmNorthOfDestination, DateTime? positionAt = null) => new()
    {
        Id = Guid.NewGuid(),
        Status = JobStatus.InProgress,
        StartedAtUtc = Now.AddHours(-1),
        DestinationLatitude = DestinationLat,
        DestinationLongitude = DestinationLon,
        LastLatitude = DestinationLat + kmNorthOfDestination / 111.0,
        LastLongitude = DestinationLon,
        LastPositionAtUtc = positionAt ?? Now.AddMinutes(-1),
    };

    [Fact]
    public void A_trip_that_just_started_announces_the_pickup()
    {
        Assert.Equal(CustomerUpdate.PickedUp, CustomerUpdates.Next(OnTheWay(kmNorthOfDestination: 120), Now));
    }

    [Fact]
    public void Nothing_is_sent_before_the_trip_starts()
    {
        var job = OnTheWay(5);
        job.Status = JobStatus.Planned;

        Assert.Null(CustomerUpdates.Next(job, Now));
    }

    [Fact]
    public void A_truck_within_half_an_hour_is_arriving_soon_and_skips_the_pickup_notice()
    {
        var job = OnTheWay(kmNorthOfDestination: 15);

        Assert.Equal(CustomerUpdate.ArrivingSoon, CustomerUpdates.Next(job, Now));

        CustomerUpdates.MarkHandled(job, CustomerUpdate.ArrivingSoon, Now);
        Assert.NotNull(job.CustomerPickedUpNoticeAtUtc);
        Assert.Null(CustomerUpdates.Next(job, Now));
    }

    [Fact]
    public void A_stale_position_never_promises_an_arrival()
    {
        var job = OnTheWay(kmNorthOfDestination: 5, positionAt: Now - CustomerUpdates.FreshPosition - TimeSpan.FromMinutes(1));
        job.CustomerPickedUpNoticeAtUtc = Now.AddMinutes(-50);

        Assert.Null(CustomerUpdates.Next(job, Now));
    }

    [Fact]
    public void A_truck_already_at_the_gate_is_not_arriving_soon()
    {
        var job = OnTheWay(kmNorthOfDestination: 5);
        job.CustomerPickedUpNoticeAtUtc = Now.AddMinutes(-50);
        job.DeliveryArrivedAtUtc = Now.AddMinutes(-2);

        Assert.Null(CustomerUpdates.Next(job, Now));
    }

    [Fact]
    public void A_job_without_a_destination_on_the_map_only_gets_the_pickup_and_delivery_notices()
    {
        var job = OnTheWay(kmNorthOfDestination: 5);
        job.DestinationLatitude = null;
        job.DestinationLongitude = null;

        Assert.Equal(CustomerUpdate.PickedUp, CustomerUpdates.Next(job, Now));
        CustomerUpdates.MarkHandled(job, CustomerUpdate.PickedUp, Now);
        Assert.Null(CustomerUpdates.Next(job, Now));
    }

    [Fact]
    public void A_pickup_notice_that_is_no_longer_news_is_not_sent()
    {
        var job = OnTheWay(kmNorthOfDestination: 300);
        job.StartedAtUtc = Now - CustomerUpdates.PickedUpStillNews - TimeSpan.FromMinutes(1);

        Assert.Null(CustomerUpdates.Next(job, Now));
    }

    [Fact]
    public void A_delivery_is_announced_once_even_if_earlier_notices_never_went_out()
    {
        var job = OnTheWay(0);
        job.Status = JobStatus.Delivered;
        job.DeliveredAtUtc = Now.AddMinutes(-3);

        Assert.Equal(CustomerUpdate.Delivered, CustomerUpdates.Next(job, Now));

        CustomerUpdates.MarkHandled(job, CustomerUpdate.Delivered, Now);
        Assert.Null(CustomerUpdates.Next(job, Now));
        Assert.NotNull(job.CustomerPickedUpNoticeAtUtc);
        Assert.NotNull(job.CustomerArrivingNoticeAtUtc);
    }

    [Fact]
    public void Yesterdays_delivery_is_not_announced()
    {
        var job = OnTheWay(0);
        job.Status = JobStatus.Delivered;
        job.DeliveredAtUtc = Now - CustomerUpdates.DeliveredStillNews - TimeSpan.FromMinutes(1);

        Assert.Null(CustomerUpdates.Next(job, Now));
    }

    [Fact]
    public void Marking_handled_keeps_the_first_time_it_was_handled()
    {
        var job = OnTheWay(120);
        CustomerUpdates.MarkHandled(job, CustomerUpdate.PickedUp, Now.AddMinutes(-30));
        CustomerUpdates.MarkHandled(job, CustomerUpdate.ArrivingSoon, Now);

        Assert.Equal(Now.AddMinutes(-30), job.CustomerPickedUpNoticeAtUtc);
        Assert.Equal(Now, job.CustomerArrivingNoticeAtUtc);
    }
}
