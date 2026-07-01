using System.Diagnostics.Metrics;
using OmniOps.Core.Interfaces;

namespace OmniOps.Infrastructure.Observability;

public sealed class TelemetryMetrics : ITelemetryMetrics, IDisposable
{
    public const string MeterName = "OmniOps.Telemetry";

    private readonly Meter _meter;
    private readonly Counter<long> _processed;
    private readonly Counter<long> _duplicatesSkipped;
    private readonly Counter<long> _anomaliesDetected;
    private readonly Counter<long> _dlqRouted;
    private readonly Counter<long> _simulatePacketsPublished;

    public TelemetryMetrics()
    {
        _meter = new Meter(MeterName);
        _processed = _meter.CreateCounter<long>(
            "telemetry.processed",
            description: "Telemetry packets persisted and broadcast successfully");
        _duplicatesSkipped = _meter.CreateCounter<long>(
            "telemetry.duplicate_skipped",
            description: "Telemetry packets skipped by Redis deduplication");
        _anomaliesDetected = _meter.CreateCounter<long>(
            "telemetry.anomaly_detected",
            description: "Anomaly events raised during telemetry processing");
        _dlqRouted = _meter.CreateCounter<long>(
            "kafka.dlq_routed",
            description: "Messages routed to the Kafka dead-letter queue");
        _simulatePacketsPublished = _meter.CreateCounter<long>(
            "simulate.packets_published",
            description: "Mock telemetry packets published via the simulate endpoint");
    }

    public void RecordTelemetryProcessed() => _processed.Add(1);

    public void RecordDuplicateSkipped() => _duplicatesSkipped.Add(1);

    public void RecordAnomalyDetected() => _anomaliesDetected.Add(1);

    public void RecordDlqRouted(string reason) =>
        _dlqRouted.Add(1, new KeyValuePair<string, object?>("reason", NormalizeReason(reason)));

    public void RecordSimulatePacketsPublished(int count)
    {
        if (count > 0)
        {
            _simulatePacketsPublished.Add(count);
        }
    }

    public void Dispose() => _meter.Dispose();

    private static string NormalizeReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return "unknown";
        }

        return reason.Length <= 64 ? reason : reason[..64];
    }
}
