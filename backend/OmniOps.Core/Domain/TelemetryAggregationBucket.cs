namespace OmniOps.Core.Domain;

public record TelemetryAggregationBucket(
    DateTime BucketStartUtc,
    double AvgSpeed,
    double AvgTemperature,
    double MinTemperature,
    double MaxTemperature,
    int SampleCount);

public record HeatmapBucket(
    double CellLatitude,
    double CellLongitude,
    double AvgSpeed,
    int PointCount);
