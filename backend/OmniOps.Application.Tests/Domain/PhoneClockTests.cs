using OmniOps.Core.Domain;
using OmniOps.Core.Entities;
using OmniOps.Core.Enums;

namespace OmniOps.Application.Tests.Domain;

public class PhoneClockTests
{
    private static readonly DateTime Now = new(2026, 11, 22, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Created = Now.AddHours(-6);

    [Fact]
    public void NoReportedTime_IsNow() => Assert.Equal(Now, PhoneClock.Resolve(null, Created, Now));

    [Fact]
    public void ATimeFromWhileOffline_IsKept()
    {
        var tapped = new DateTimeOffset(Now.AddMinutes(-47));
        Assert.Equal(Now.AddMinutes(-47), PhoneClock.Resolve(tapped, Created, Now));
    }

    [Fact]
    public void LocalTimeWithAnOffset_BecomesUtc()
    {
        var local = new DateTimeOffset(2026, 11, 22, 13, 30, 0, TimeSpan.FromHours(2)); // 11:30 UTC
        Assert.Equal(Now.AddMinutes(-30), PhoneClock.Resolve(local, Created, Now));
    }

    [Fact]
    public void AClockSlightlyAhead_IsNow_AndOneFarAhead_IsIgnored()
    {
        Assert.Equal(Now, PhoneClock.Resolve(new DateTimeOffset(Now.AddSeconds(50)), Created, Now));
        Assert.Equal(Now, PhoneClock.Resolve(new DateTimeOffset(Now.AddDays(3)), Created, Now));
    }

    [Fact]
    public void ATimeBeforeTheJobExisted_IsMovedUpToIt() =>
        Assert.Equal(Created, PhoneClock.Resolve(new DateTimeOffset(Created.AddYears(-1)), Created, Now));

    [Theory]
    [InlineData(-31, false)] // too old for the hand-over
    [InlineData(-29, true)]
    [InlineData(0, true)]
    [InlineData(1, true)]    // photographed right after tapping "Delivered"
    [InlineData(10, false)]  // taken long after: does not prove the state at hand-over
    public void DeliveryReading_MustBeFromJustBeforeHandOver(int minutesFromHandOver, bool counts)
    {
        var handOver = Now.AddMinutes(-60);
        var job = new Trip { MinTempCelsius = 2, MaxTempCelsius = 8 };
        job.Documents.Add(new TripDocument
        {
            Kind = TripDocumentKind.TemperaturePhoto,
            TemperatureCelsius = 5,
            CapturedAtUtc = handOver.AddMinutes(minutesFromHandOver),
        });

        Assert.Equal(counts, JobTemperaturePolicy.HasRecentReading(job, handOver));
    }
}
