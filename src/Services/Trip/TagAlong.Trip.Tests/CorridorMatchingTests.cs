using NetTopologySuite.Geometries;
using NetTopologySuite.LinearReferencing;
using NetTopologySuite.Operation.Distance;
using Xunit;

namespace TagAlong.Trip.Tests;

public class CorridorMatchingTests
{
    private static readonly GeometryFactory Factory = new(new PrecisionModel(), 4326);

    // Lagos trip: 19 Bajulaiye → 4 Balogun Ipaja passing through Maryland
    private const double OriginLat = 6.5324792, OriginLon = 3.3833306;
    private const double DestLat = 6.612122, DestLon = 3.2608187;
    // Maryland approximate coordinates
    private const double MarylandLat = 6.5520, MarylandLon = 3.3556;

    private static LineString BuildStraightRoute()
    {
        return Factory.CreateLineString(new[]
        {
            new Coordinate(OriginLon, OriginLat),
            new Coordinate(MarylandLon, MarylandLat),
            new Coordinate(DestLon, DestLat)
        });
    }

    [Fact]
    public void PointOnRoute_IsWithinRadius_ReturnsTrue()
    {
        var route = BuildStraightRoute();
        var marylandPoint = new Point(MarylandLon, MarylandLat) { SRID = 4326 };

        var nearest = DistanceOp.NearestPoints(route, marylandPoint);
        var distDeg = Math.Sqrt(
            Math.Pow(nearest[0].X - MarylandLon, 2) +
            Math.Pow(nearest[0].Y - MarylandLat, 2));
        // Same point — distance should be 0
        Assert.True(distDeg < 0.001);
    }

    [Fact]
    public void PointFarFromRoute_IsOutsideRadius()
    {
        var route = BuildStraightRoute();
        // Abuja is far from Lagos (~500km)
        const double abujaLat = 9.0765, abujaLon = 7.3986;
        var abujaPoint = new Point(abujaLon, abujaLat) { SRID = 4326 };

        var nearest = DistanceOp.NearestPoints(route, abujaPoint);
        var closestOnRoute = nearest[0];
        var distKm = Haversine(closestOnRoute.Y, closestOnRoute.X, abujaLat, abujaLon);

        Assert.True(distKm > 200, $"Expected dist > 200 km, got {distKm:F1}");
    }

    [Fact]
    public void DirectionCheck_MarylandIsAfterOrigin()
    {
        var route = BuildStraightRoute();
        var indexedLine = new LocationIndexedLine(route);

        var originLoc = indexedLine.Project(new Coordinate(OriginLon, OriginLat));
        var marylandLoc = indexedLine.Project(new Coordinate(MarylandLon, MarylandLat));

        // Maryland should come after the origin along the route
        Assert.True(marylandLoc.CompareTo(originLoc) >= 0,
            "Maryland should be at or after origin along the route");
    }

    [Fact]
    public void DirectionCheck_DestinationIsAfterMidpoint()
    {
        var route = BuildStraightRoute();
        var indexedLine = new LocationIndexedLine(route);

        var marylandLoc = indexedLine.Project(new Coordinate(MarylandLon, MarylandLat));
        var destLoc = indexedLine.Project(new Coordinate(DestLon, DestLat));

        Assert.True(destLoc.CompareTo(marylandLoc) >= 0,
            "Trip destination should come after Maryland along the route");
    }

    [Fact]
    public void FallbackPointToSegment_MarylandIsNearRoute()
    {
        // Without a stored RouteLine, falls back to point-to-segment on straight line
        var distKm = PointToSegmentKm(MarylandLat, MarylandLon, OriginLat, OriginLon, DestLat, DestLon);
        Assert.True(distKm <= 20, $"Maryland should be within 20 km of route, got {distKm:F2}");
    }

    private static double PointToSegmentKm(
        double pLat, double pLon,
        double aLat, double aLon,
        double bLat, double bLon)
    {
        const double KmPerDeg = 111.0;
        double cosLat = Math.Cos((aLat + bLat) / 2 * Math.PI / 180);
        double px = (pLon - aLon) * KmPerDeg * cosLat;
        double py = (pLat - aLat) * KmPerDeg;
        double bx = (bLon - aLon) * KmPerDeg * cosLat;
        double by = (bLat - aLat) * KmPerDeg;
        double segLenSq = bx * bx + by * by;
        if (segLenSq < 1e-10) return Math.Sqrt(px * px + py * py);
        double t = Math.Clamp((px * bx + py * by) / segLenSq, 0.0, 1.0);
        double dx = px - t * bx, dy = py - t * by;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static double Haversine(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371.0;
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLon = (lon2 - lon1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180)
              * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }
}
