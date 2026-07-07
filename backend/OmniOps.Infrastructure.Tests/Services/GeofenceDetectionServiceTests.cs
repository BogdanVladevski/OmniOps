using OmniOps.Core.Entities;
using OmniOps.Core.Enums;
using OmniOps.Infrastructure.Services;
using Xunit;

namespace OmniOps.Infrastructure.Tests.Services;

public class GeofenceDetectionServiceTests
{
    [Fact]
    public void IsInsideRadius_ReturnsTrue_WhenPointWithinRadius()
    {
        var geofence = new Geofence
        {
            ShapeType = GeofenceShapeType.Radius,
            CenterLatitude = 40.0,
            CenterLongitude = -75.0,
            RadiusMeters = 1000
        };

        Assert.True(GeofenceDetectionService.IsInside(geofence, 40.001, -75.0));
    }

    [Fact]
    public void IsInsidePolygon_ReturnsTrue_ForPointInsideSquare()
    {
        var geofence = new Geofence
        {
            ShapeType = GeofenceShapeType.Polygon,
            PolygonCoordinatesJson = "[[0,0],[0,1],[1,1],[1,0]]"
        };

        Assert.True(GeofenceDetectionService.IsInside(geofence, 0.5, 0.5));
        Assert.False(GeofenceDetectionService.IsInside(geofence, 2.0, 2.0));
    }
}
