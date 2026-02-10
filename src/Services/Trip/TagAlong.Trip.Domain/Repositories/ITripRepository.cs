using TagAlong.Trip.Domain.Entities;
using TripEntity = TagAlong.Trip.Domain.Entities.Trip;

namespace TagAlong.Trip.Domain.Repositories;

public interface ITripRepository
{
    Task<TripEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TripEntity?> GetByIdWithStopsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<TripEntity>> GetByTravelerIdAsync(Guid travelerId, CancellationToken cancellationToken = default);
    Task<IEnumerable<TripEntity>> SearchTripsAsync(
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
        CancellationToken cancellationToken = default);
    Task<IEnumerable<TripEntity>> GetActiveTripsAsync(CancellationToken cancellationToken = default);
    Task AddAsync(TripEntity trip, CancellationToken cancellationToken = default);
    void Update(TripEntity trip);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
