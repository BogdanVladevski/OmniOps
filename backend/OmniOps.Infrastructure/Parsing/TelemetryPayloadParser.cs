using System.Text.Json;
using OmniOps.Core.Entities;

namespace OmniOps.Infrastructure.Parsing;

public static class TelemetryPayloadParser
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static VehicleTelemetry? TryParse(string json)
    {
        var telemetry = JsonSerializer.Deserialize<VehicleTelemetry>(json, SerializerOptions);
        if (telemetry is not null && !string.IsNullOrWhiteSpace(telemetry.VehicleId))
        {
            return telemetry;
        }

        using var document = JsonDocument.Parse(json);
        if (TryGetNestedTelemetryElement(document.RootElement, out var telemetryElement))
        {
            return JsonSerializer.Deserialize<VehicleTelemetry>(telemetryElement.GetRawText(), SerializerOptions);
        }

        return telemetry;
    }

    private static bool TryGetNestedTelemetryElement(JsonElement root, out JsonElement telemetryElement)
    {
        if (root.TryGetProperty("Telemetry", out telemetryElement)
            || root.TryGetProperty("telemetry", out telemetryElement))
        {
            return true;
        }

        telemetryElement = default;
        return false;
    }

    public static bool IsValidTelemetry(VehicleTelemetry? telemetry) =>
        telemetry is not null && !string.IsNullOrWhiteSpace(telemetry.VehicleId);
}
