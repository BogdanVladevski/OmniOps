namespace OmniOps.Core.Domain;

public record TelemetryAggregationBucket(
    DateTime BucketStartUtc,
    double AvgSpeed,
    double? AvgTemperature,   // null when no reading in the bucket carried a cargo temperature
    double? MinTemperature,
    double? MaxTemperature,
    int SampleCount);

public record HeatmapBucket(
    double CellLatitude,
    double CellLongitude,
    double AvgSpeed,
    int PointCount);
