namespace OmniOps.UnitTests.Domain;

public class JobRulesTests
{
    private static readonly DateTime Now = new(2026, 11, 22, 12, 0, 0, DateTimeKind.Utc);

    private static Job Refrigerated() => new()
    {
        Id = Guid.NewGuid(), Status = JobStatus.InProgress, StartedAtUtc = Now.AddHours(-3), MinTempCelsius = 2, MaxTempCelsius = 8,
    };

    private static void AddReading(Job job, double celsius, DateTime at) =>
        job.Documents.Add(new JobDocument { Kind = JobDocumentKind.TemperaturePhoto, TemperatureCelsius = celsius, CapturedAtUtc = at });

    [Fact]
    public void JobSummary_CountsOutOfRangeReadings_AndSchedulesTheNextCheck()
    {
        var job = Refrigerated();
        AddReading(job, 4, Now.AddHours(-2));
        AddReading(job, 9.5, Now.AddMinutes(-30));

        var dto = JobDto.From(job);

        Assert.Equal(2, dto.TemperatureReadingCount);
        Assert.Equal(1, dto.TemperatureOutOfRangeCount);
        Assert.Equal(9.5, dto.LastTemperatureCelsius);
        Assert.Equal(Now.AddMinutes(-30) + JobTemperaturePolicy.CheckInterval, dto.TemperatureCheckDueUtc);
    }

    [Fact]
    public void JobSummary_WithoutARange_HasNoTemperatureSchedule() =>
        Assert.Null(JobDto.From(new Job { Status = JobStatus.InProgress }).TemperatureCheckDueUtc);

    [Fact]
    public void BeforeAnyReading_ACheckIsDueAsSoonAsTheJobStarts() =>
        Assert.Equal(Now.AddHours(-3), JobTemperaturePolicy.NextCheckDueUtc(Refrigerated()));

    [Fact]
    public void Eta_SkopjeToBitola_IsAboutTwoAndAHalfHoursAtTruckSpeed()
    {
        var (km, eta) = DeliveryEta.Estimate(41.9981, 21.4254, 41.0297, 21.3292, Now);

        // ~108 km straight line × 1.3 road factor ≈ 140 km; at 60 km/h ≈ 2 h 20 min.
        Assert.InRange(km, 130, 150);
        Assert.InRange((eta - Now).TotalMinutes, 130, 150);
    }

    [Fact]
    public void Eta_AtTheDoor_IsNow()
    {
        var (km, eta) = DeliveryEta.Estimate(41.9981, 21.4254, 41.9990, 21.4260, Now);
        Assert.Equal(0, km);
        Assert.Equal(Now, eta);
    }

    [Theory]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAA", true)]
    [InlineData("short", false)]
    [InlineData("../../etc/passwd/xxxxxxxxx", false)]
    [InlineData("", false)]
    public void PublicTokens_AreCheckedBeforeAnyLookup(string token, bool looksValid) =>
        Assert.Equal(looksValid, PublicToken.LooksValid(token));

    [Fact]
    public void PublicTokens_AreUnguessableAndUrlSafe()
    {
        var tokens = Enumerable.Range(0, 100).Select(_ => PublicToken.Create()).ToList();
        Assert.Equal(100, tokens.Distinct().Count());
        Assert.All(tokens, t => Assert.True(PublicToken.LooksValid(t)));
    }
}
