using OmniOps.Infrastructure.Parsing;

namespace OmniOps.Infrastructure.Tests.Parsing;

public class TelemetryPayloadParserTests
{
    [Fact]
    public void TryParse_WithValidFlatJson_ReturnsTelemetry()
    {
        const string json = """
            {
              "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
              "vehicleId": "Truck-001",
              "latitude": 41.99,
              "longitude": 21.43,
              "speed": 80,
              "fuelLevel": 75,
              "engineTemperature": 90,
              "timestamp": "2026-06-30T12:00:00Z"
            }
            """;

        var result = TelemetryPayloadParser.TryParse(json);

        Assert.NotNull(result);
        Assert.Equal("Truck-001", result!.VehicleId);
        Assert.Equal(41.99, result.Latitude);
        Assert.True(TelemetryPayloadParser.IsValidTelemetry(result));
    }

    [Fact]
    public void TryParse_WithMissingVehicleId_ReturnsInvalidTelemetry()
    {
        const string json = """
            {
              "latitude": 41.99,
              "longitude": 21.43,
              "speed": 80
            }
            """;

        var result = TelemetryPayloadParser.TryParse(json);

        Assert.False(TelemetryPayloadParser.IsValidTelemetry(result));
    }

    [Fact]
    public void TryParse_WithLegacyNestedTelemetryProperty_UnwrapsPayload()
    {
        const string json = """
            {
              "type": "TelemetryReceivedEvent",
              "telemetry": {
                "vehicleId": "Truck-002",
                "latitude": 42.0,
                "longitude": 21.5,
                "speed": 60,
                "fuelLevel": 50,
                "engineTemperature": 85,
                "timestamp": "2026-06-30T12:00:00Z"
              }
            }
            """;

        var result = TelemetryPayloadParser.TryParse(json);

        Assert.NotNull(result);
        Assert.Equal("Truck-002", result!.VehicleId);
        Assert.True(TelemetryPayloadParser.IsValidTelemetry(result));
    }

    [Fact]
    public void TryParse_WithMalformedJson_ThrowsJsonException()
    {
        Assert.Throws<System.Text.Json.JsonException>(() =>
            TelemetryPayloadParser.TryParse("{ not valid json"));
    }
}
