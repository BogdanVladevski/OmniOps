using Microsoft.Extensions.Logging.Abstractions;
using OmniOps.Core.Domain;
using OmniOps.Core.Enums;
using OmniOps.Infrastructure.Services.Rag;

namespace OmniOps.Infrastructure.Tests.Services.Rag;

public class KeywordPlaybookRetrievalServiceTests
{
    private readonly KeywordPlaybookRetrievalService _service;

    public KeywordPlaybookRetrievalServiceTests()
    {
        // Resolve the real docs/playbooks corpus from the repo root relative to the test output dir.
        var directory = LocatePlaybooksDirectory();
        var loader = new PlaybookLoader(directory, NullLogger<PlaybookLoader>.Instance);
        _service = new KeywordPlaybookRetrievalService(loader);
    }

    [Fact]
    public void Retrieve_InsulinCriticalExcursion_PrefersInsulinExcursionSop()
    {
        var incident = new IncidentContext
        {
            VehicleId = "Truck-001",
            Severity = AnomalySeverity.Critical,
            ExcursionDurationSeconds = 20,
            ProductName = "Insulin Glargine",
            IncidentSummary = "excursion"
        };

        var result = _service.Retrieve(incident);

        Assert.NotEmpty(result);
        Assert.Equal("SOP-CCM-7.3", result[0].Id);
    }

    [Fact]
    public void Retrieve_VaccineWarningTrend_PrefersEarlyWarningSop()
    {
        var incident = new IncidentContext
        {
            VehicleId = "Truck-002",
            Severity = AnomalySeverity.Warning,
            ExcursionDurationSeconds = 0,
            ProductName = "Hepatitis B Vaccine",
            IncidentSummary = "trending"
        };

        var result = _service.Retrieve(incident);

        Assert.Contains(result, doc => doc.Id == "SOP-CCM-6.1");
    }

    [Fact]
    public void Retrieve_ProlongedCriticalBreach_IncludesProlongedBreakSop()
    {
        var incident = new IncidentContext
        {
            VehicleId = "Truck-001",
            Severity = AnomalySeverity.Critical,
            ExcursionDurationSeconds = 120,
            ProductName = "Insulin Glargine",
            IncidentSummary = "excursion"
        };

        var result = _service.Retrieve(incident, maxResults: 2);

        Assert.Contains(result, doc => doc.Id == "SOP-CCM-8.2");
    }

    [Fact]
    public void Retrieve_ShortExcursion_DoesNotSelectProlongedBreakSop()
    {
        var incident = new IncidentContext
        {
            VehicleId = "Truck-001",
            Severity = AnomalySeverity.Critical,
            ExcursionDurationSeconds = 10,
            ProductName = "Insulin Glargine",
            IncidentSummary = "excursion"
        };

        var result = _service.Retrieve(incident, maxResults: 2);

        Assert.DoesNotContain(result, doc => doc.Id == "SOP-CCM-8.2");
    }

    [Fact]
    public void Retrieve_AlwaysReturnsAtLeastOneDocument()
    {
        var incident = new IncidentContext
        {
            VehicleId = "Truck-009",
            Severity = AnomalySeverity.Warning,
            ExcursionDurationSeconds = 0,
            ProductName = null,
            IncidentSummary = "unknown"
        };

        var result = _service.Retrieve(incident);

        Assert.NotEmpty(result);
    }

    private static string LocatePlaybooksDirectory()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "docs", "playbooks");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate docs/playbooks from the test output directory.");
    }
}
