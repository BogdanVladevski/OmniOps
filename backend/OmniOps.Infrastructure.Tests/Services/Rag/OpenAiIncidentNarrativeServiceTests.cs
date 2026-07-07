using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OmniOps.Core.Domain;
using OmniOps.Core.Enums;
using OmniOps.Infrastructure.Configuration;
using OmniOps.Infrastructure.Services.Rag;

namespace OmniOps.Infrastructure.Tests.Services.Rag;

public class OpenAiIncidentNarrativeServiceTests
{
    private static readonly IReadOnlyList<PlaybookDocument> Playbooks = new[]
    {
        new PlaybookDocument
        {
            Id = "SOP-CCM-7.3",
            Title = "Temperature Excursion Response — Insulin",
            Severity = AnomalySeverity.Critical,
            Body = "Isolate shipment, notify QA, quarantine assessment."
        }
    };

    private static IncidentContext Incident() => new()
    {
        VehicleId = "Truck-001",
        Severity = AnomalySeverity.Critical,
        ExcursionDurationSeconds = 90,
        TemperatureCelsius = 103,
        ProductName = "Insulin Glargine",
        BatchNumber = "B-4471",
        ValueAtRiskUsd = 12_400m,
        IncidentSummary = "[CRITICAL] Insulin Glargine batch B-4471 excursion"
    };

    [Fact]
    public async Task GenerateNarrative_WhenLlmDisabled_ReturnsLabeledFallback()
    {
        var service = CreateService(
            new LlmOptions { Enabled = false },
            new StubHttpMessageHandler(_ => throw new InvalidOperationException("should not be called")));

        var narrative = await service.GenerateNarrativeAsync(Incident(), Playbooks);

        Assert.Contains("AI narrative unavailable", narrative);
        Assert.Contains("SOP-CCM-7.3", narrative);
    }

    [Fact]
    public async Task GenerateNarrative_WhenLlmCallThrows_FallsBackWithoutThrowing()
    {
        var service = CreateService(
            new LlmOptions { Enabled = true, ApiKey = "test-key" },
            new StubHttpMessageHandler(_ => throw new HttpRequestException("network down")));

        var narrative = await service.GenerateNarrativeAsync(Incident(), Playbooks);

        Assert.Contains("AI narrative unavailable", narrative);
        Assert.Contains("SOP-CCM-7.3", narrative);
    }

    [Fact]
    public async Task GenerateNarrative_WhenLlmReturnsError_FallsBackCleanly()
    {
        var service = CreateService(
            new LlmOptions { Enabled = true, ApiKey = "test-key" },
            new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)));

        var narrative = await service.GenerateNarrativeAsync(Incident(), Playbooks);

        Assert.Contains("AI narrative unavailable", narrative);
    }

    [Fact]
    public async Task GenerateNarrative_WhenLlmSucceeds_ReturnsModelContent()
    {
        const string modelText = "Halt Truck-001 immediately per SOP-CCM-7.3 and quarantine batch B-4471.";
        var responseJson = $$"""
            { "choices": [ { "message": { "role": "assistant", "content": "{{modelText}}" } } ] }
            """;

        var service = CreateService(
            new LlmOptions { Enabled = true, ApiKey = "test-key" },
            new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json")
            }));

        var narrative = await service.GenerateNarrativeAsync(Incident(), Playbooks);

        Assert.Equal(modelText, narrative);
        Assert.DoesNotContain("AI narrative unavailable", narrative);
    }

    private static OpenAiIncidentNarrativeService CreateService(LlmOptions options, StubHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler);
        return new OpenAiIncidentNarrativeService(
            httpClient,
            Options.Create(options),
            NullLogger<OpenAiIncidentNarrativeService>.Instance);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(_responder(request));
        }
    }
}
