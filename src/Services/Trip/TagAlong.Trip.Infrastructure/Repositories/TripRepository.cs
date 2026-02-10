using Microsoft.EntityFrameworkCore;
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
        CancellationToken cancellationToken = default)
    {
        var query = _context.Trips
            .Include(t => t.Stops)
            .Where(t => t.Status == TripStatus.Scheduled && t.DepartureTime > DateTime.UtcNow);

        if (!string.IsNullOrEmpty(origin))
        {
            query = query.Where(t => t.Origin.ToLower().Contains(origin.ToLower()));
        }

        if (!string.IsNullOrEmpty(destination))
        {
            query = query.Where(t => t.Destination.ToLower().Contains(destination.ToLower()));
        }

        if (departureDate.HasValue)
        {
            var startOfDay = departureDate.Value.Date;
            var endOfDay = startOfDay.AddDays(1);
            query = query.Where(t => t.DepartureTime >= startOfDay && t.DepartureTime < endOfDay);
        }

        // Note: For proper geospatial queries, consider using PostGIS extension
        // This is a simplified version
        if (originLat.HasValue && originLon.HasValue)
        {
            var minLat = originLat.Value - (radiusKm / 111.0);
            var maxLat = originLat.Value + (radiusKm / 111.0);
            var minLon = originLon.Value - (radiusKm / (111.0 * Math.Cos(originLat.Value * Math.PI / 180)));
            var maxLon = originLon.Value + (radiusKm / (111.0 * Math.Cos(originLat.Value * Math.PI / 180)));

            query = query.Where(t =>
                t.OriginLatitude >= minLat && t.OriginLatitude <= maxLat &&
                t.OriginLongitude >= minLon && t.OriginLongitude <= maxLon);
        }

        if (destLat.HasValue && destLon.HasValue)
        {
            var minLat = destLat.Value - (radiusKm / 111.0);
            var maxLat = destLat.Value + (radiusKm / 111.0);
            var minLon = destLon.Value - (radiusKm / (111.0 * Math.Cos(destLat.Value * Math.PI / 180)));
            var maxLon = destLon.Value + (radiusKm / (111.0 * Math.Cos(destLat.Value * Math.PI / 180)));

            query = query.Where(t =>
                t.DestinationLatitude >= minLat && t.DestinationLatitude <= maxLat &&
                t.DestinationLongitude >= minLon && t.DestinationLongitude <= maxLon);
        }

        return await query
            .OrderBy(t => t.DepartureTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
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
