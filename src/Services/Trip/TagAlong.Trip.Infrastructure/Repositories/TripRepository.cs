using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using NetTopologySuite.LinearReferencing;
using NetTopologySuite.Operation.Distance;
using TagAlong.Trip.Domain.Entities;
using TagAlong.Trip.Domain.Repositories;
using TagAlong.Trip.Infrastructure.Persistence;

namespace TagAlong.Trip.Infrastructure.Repositories;

public class TripRepository : ITripRepository
{
    private readonly TripDbContext _context;

    public TripRepository(TripDbContext context)
    {
        _context = context;
    }

    public async Task<Domain.Entities.Trip?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Trips.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<Domain.Entities.Trip?> GetByIdWithStopsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Trips
            .Include(t => t.Stops)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<IEnumerable<Domain.Entities.Trip>> GetByTravelerIdAsync(Guid travelerId, CancellationToken cancellationToken = default)
    {
        return await _context.Trips
            .Include(t => t.Stops)
            .Where(t => t.TravelerId == travelerId)
            .OrderByDescending(t => t.DepartureTime)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Domain.Entities.Trip>> SearchTripsAsync(
        string? origin,
        string? destination,
        DateTime? departureDate,
        double? originLat,
        double? originLon,
        double? destLat,
        double? destLon,
        double radiusKm,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default,
        Domain.Entities.TripType? tripType = null)
    {
        var query = _context.Trips
            .Include(t => t.Stops)
            .Where(t => t.Status == TripStatus.Scheduled && t.DepartureTime.Date >= DateTime.UtcNow.Date);

        if (tripType.HasValue)
            query = query.Where(t => t.TripType == tripType.Value);

        if (!string.IsNullOrEmpty(origin))
            query = query.Where(t => t.Origin.ToLower().Contains(origin.ToLower()));

        if (departureDate.HasValue)
        {
            var startOfDay = departureDate.Value.Date;
            var endOfDay = startOfDay.AddDays(1);
            query = query.Where(t => t.DepartureTime >= startOfDay && t.DepartureTime < endOfDay);
        }

        // SQL bounding-box pre-filter for trip origin (fast, approximate).
        if (originLat.HasValue && originLon.HasValue)
        {
            var latDelta = radiusKm / 111.0;
            var lonDelta = radiusKm / (111.0 * Math.Cos(originLat.Value * Math.PI / 180));
            query = query.Where(t =>
                t.OriginLatitude  >= originLat.Value - latDelta && t.OriginLatitude  <= originLat.Value + latDelta &&
                t.OriginLongitude >= originLon.Value - lonDelta && t.OriginLongitude <= originLon.Value + lonDelta);
        }

        // SQL loose pre-filter for destination: keep any trip whose route bounding
        // box (expanded by radiusKm) contains the passenger's destination.
        // The exact "along the route" check is done in-memory below.
        if (destLat.HasValue && destLon.HasValue)
        {
            var latTol = radiusKm / 111.0;
            var lonTol = radiusKm / (111.0 * Math.Cos(destLat.Value * Math.PI / 180));
            query = query.Where(t =>
                destLat.Value >= Math.Min(t.OriginLatitude,  t.DestinationLatitude)  - latTol &&
                destLat.Value <= Math.Max(t.OriginLatitude,  t.DestinationLatitude)  + latTol &&
                destLon.Value >= Math.Min(t.OriginLongitude, t.DestinationLongitude) - lonTol &&
                destLon.Value <= Math.Max(t.OriginLongitude, t.DestinationLongitude) + lonTol);
        }

        var candidates = await query
            .OrderBy(t => t.DepartureTime)
            .ToListAsync(cancellationToken);

        // Exact in-memory filter: pickup must be near trip origin.
        if (originLat.HasValue && originLon.HasValue)
            candidates = candidates
                .Where(t => HaversineKm(originLat.Value, originLon.Value, t.OriginLatitude, t.OriginLongitude) <= radiusKm)
                .ToList();

        // Exact in-memory filter: user's destination must be along the route.
        // For trips with a stored RouteLine, use NTS distance to the polyline.
        // For trips without one, fall back to straight point-to-segment distance.
        if (destLat.HasValue && destLon.HasValue)
        {
            var destPoint = new Point(destLon.Value, destLat.Value) { SRID = 4326 };
            candidates = candidates
                .Where(t =>
                {
                    if (t.RouteLine is not null && t.RouteStatus == TripRouteStatus.Stored)
                        return CorridorMatchKm(t, destPoint, destLat.Value, destLon.Value, radiusKm);

                    return PointToSegmentKm(
                        destLat.Value, destLon.Value,
                        t.OriginLatitude, t.OriginLongitude,
                        t.DestinationLatitude, t.DestinationLongitude) <= radiusKm;
                })
                .ToList();
        }

        return candidates
            .Skip((page - 1) * pageSize)
            .Take(pageSize);
    }

    // Haversine great-circle distance between two points (km).
    private static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371.0;
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLon = (lon2 - lon1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180)
              * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    // Shortest distance (km) from point P to segment A→B.
    // Uses a local flat-Earth projection centred on A — accurate for distances < ~200 km.
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
        if (segLenSq < 1e-10)
            return Math.Sqrt(px * px + py * py); // A == B, distance to that point

        // Project P onto the segment, clamp t to [0,1] so we stay on the segment.
        double t = Math.Clamp((px * bx + py * by) / segLenSq, 0.0, 1.0);
        double dx = px - t * bx;
        double dy = py - t * by;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    // Check if destPoint is within radiusKm of the trip's stored route polyline,
    // and that the destination appears AFTER the pickup origin along the route.
    private static bool CorridorMatchKm(
        Domain.Entities.Trip trip,
        Point destPoint,
        double destLat, double destLon,
        double radiusKm)
    {
        var route = trip.RouteLine!;

        // Find closest point on route to user's destination.
        var nearest = DistanceOp.NearestPoints(route, destPoint);
        var closestOnRoute = nearest[0]; // point on the line
        var distKm = HaversineKm(closestOnRoute.Y, closestOnRoute.X, destLat, destLon);
        if (distKm > radiusKm)
            return false;

        // Direction check: user destination must come AFTER the trip's origin along the route
        // (i.e. the passenger won't be dropped off before they board).
        var indexedLine = new LocationIndexedLine(route);
        var originCoord = new Coordinate(trip.OriginLongitude, trip.OriginLatitude);
        var destCoord = new Coordinate(destLon, destLat);

        var originLoc = indexedLine.Project(originCoord);
        var destLoc = indexedLine.Project(destCoord);

        // destLoc must come strictly after originLoc (or at least not before the start).
        return destLoc.CompareTo(originLoc) >= 0;
    }

    public async Task<IEnumerable<Domain.Entities.Trip>> GetActiveTripsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Trips
            .Include(t => t.Stops)
            .Where(t => t.Status == TripStatus.Scheduled || t.Status == TripStatus.InProgress)
            .OrderBy(t => t.DepartureTime)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Domain.Entities.Trip trip, CancellationToken cancellationToken = default)
    {
        await _context.Trips.AddAsync(trip, cancellationToken);
    }

    public void Update(Domain.Entities.Trip trip)
    {
        _context.Trips.Update(trip);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
