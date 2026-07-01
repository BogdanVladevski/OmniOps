namespace OmniOps.Core.Interfaces;

public interface ITelemetryMetrics
{
    void RecordTelemetryProcessed();
    void RecordDuplicateSkipped();
    void RecordAnomalyDetected();
    void RecordDlqRouted(string reason);
    void RecordSimulatePacketsPublished(int count);
}
