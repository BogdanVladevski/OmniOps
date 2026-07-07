using OmniOps.Core.Enums;

namespace OmniOps.Core.Entities;

public class Geofence
{
    public Guid Id { get; set; }
    public Guid? FleetId { get; set; }
    public string Name { get; set; } = string.Empty;
    public GeofenceShapeType ShapeType { get; set; }
    public double? CenterLatitude { get; set; }
    public double? CenterLongitude { get; set; }
    public double? RadiusMeters { get; set; }
    public string? PolygonCoordinatesJson { get; set; }
    public bool IsActive { get; set; } = true;
    public Fleet? Fleet { get; set; }
}
