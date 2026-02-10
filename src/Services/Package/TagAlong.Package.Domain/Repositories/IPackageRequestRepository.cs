using TagAlong.Package.Domain.Entities;

namespace TagAlong.Package.Domain.Repositories;

public interface IPackageRequestRepository
{
    Task<PackageRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<PackageRequest>> GetBySenderIdAsync(Guid senderId, CancellationToken cancellationToken = default);
    Task<IEnumerable<PackageRequest>> SearchOpenRequestsAsync(
        string? pickupLocation,
        string? deliveryLocation,
        double? pickupLat,
        double? pickupLon,
        double? deliveryLat,
        double? deliveryLon,
        double radiusKm,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task<IEnumerable<PackageRequest>> GetMatchingRequestsForTripAsync(
        double originLat,
        double originLon,
        double destLat,
        double destLon,
        List<(double lat, double lon)> stops,
        double radiusKm,
        CancellationToken cancellationToken = default);
    Task AddAsync(PackageRequest request, CancellationToken cancellationToken = default);
    void Update(PackageRequest request);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
