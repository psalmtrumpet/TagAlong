using Microsoft.EntityFrameworkCore;
using TagAlong.Package.Domain.Entities;
using TagAlong.Package.Domain.Repositories;
using TagAlong.Package.Infrastructure.Persistence;

namespace TagAlong.Package.Infrastructure.Repositories;

public class PackageRequestRepository : IPackageRequestRepository
{
    private readonly PackageDbContext _context;

    public PackageRequestRepository(PackageDbContext context)
    {
        _context = context;
    }

    public async Task<PackageRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.PackageRequests.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<IEnumerable<PackageRequest>> GetBySenderIdAsync(Guid senderId, CancellationToken cancellationToken = default)
    {
        return await _context.PackageRequests
            .Where(p => p.SenderId == senderId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<PackageRequest>> SearchOpenRequestsAsync(
        string? pickupLocation,
        string? deliveryLocation,
        double? pickupLat,
        double? pickupLon,
        double? deliveryLat,
        double? deliveryLon,
        double radiusKm,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.PackageRequests.Where(p => p.Status == PackageRequestStatus.Open);

        if (!string.IsNullOrEmpty(pickupLocation))
        {
            query = query.Where(p => p.PickupLocation.ToLower().Contains(pickupLocation.ToLower()));
        }

        if (!string.IsNullOrEmpty(deliveryLocation))
        {
            query = query.Where(p => p.DeliveryLocation.ToLower().Contains(deliveryLocation.ToLower()));
        }

        if (pickupLat.HasValue && pickupLon.HasValue)
        {
            var minLat = pickupLat.Value - (radiusKm / 111.0);
            var maxLat = pickupLat.Value + (radiusKm / 111.0);
            var minLon = pickupLon.Value - (radiusKm / (111.0 * Math.Cos(pickupLat.Value * Math.PI / 180)));
            var maxLon = pickupLon.Value + (radiusKm / (111.0 * Math.Cos(pickupLat.Value * Math.PI / 180)));

            query = query.Where(p =>
                p.PickupLatitude >= minLat && p.PickupLatitude <= maxLat &&
                p.PickupLongitude >= minLon && p.PickupLongitude <= maxLon);
        }

        if (deliveryLat.HasValue && deliveryLon.HasValue)
        {
            var minLat = deliveryLat.Value - (radiusKm / 111.0);
            var maxLat = deliveryLat.Value + (radiusKm / 111.0);
            var minLon = deliveryLon.Value - (radiusKm / (111.0 * Math.Cos(deliveryLat.Value * Math.PI / 180)));
            var maxLon = deliveryLon.Value + (radiusKm / (111.0 * Math.Cos(deliveryLat.Value * Math.PI / 180)));

            query = query.Where(p =>
                p.DeliveryLatitude >= minLat && p.DeliveryLatitude <= maxLat &&
                p.DeliveryLongitude >= minLon && p.DeliveryLongitude <= maxLon);
        }

        return await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<PackageRequest>> GetMatchingRequestsForTripAsync(
        double originLat,
        double originLon,
        double destLat,
        double destLon,
        List<(double lat, double lon)> stops,
        double radiusKm,
        CancellationToken cancellationToken = default)
    {
        // Build bounding boxes for all points on the route
        var allPoints = new List<(double lat, double lon)> { (originLat, originLon), (destLat, destLon) };
        allPoints.AddRange(stops);

        var requests = await _context.PackageRequests
            .Where(p => p.Status == PackageRequestStatus.Open)
            .ToListAsync(cancellationToken);

        // Filter in memory for complex route matching
        return requests.Where(p =>
        {
            foreach (var point in allPoints)
            {
                if (IsWithinRadius(p.PickupLatitude, p.PickupLongitude, point.lat, point.lon, radiusKm))
                {
                    foreach (var destPoint in allPoints)
                    {
                        if (IsWithinRadius(p.DeliveryLatitude, p.DeliveryLongitude, destPoint.lat, destPoint.lon, radiusKm))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        });
    }

    private static bool IsWithinRadius(double lat1, double lon1, double lat2, double lon2, double radiusKm)
    {
        const double R = 6371;
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c <= radiusKm;
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;

    public async Task AddAsync(PackageRequest request, CancellationToken cancellationToken = default)
    {
        await _context.PackageRequests.AddAsync(request, cancellationToken);
    }

    public void Update(PackageRequest request)
    {
        _context.PackageRequests.Update(request);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
