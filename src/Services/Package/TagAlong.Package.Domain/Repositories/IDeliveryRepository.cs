using TagAlong.Package.Domain.Entities;

namespace TagAlong.Package.Domain.Repositories;

public interface IDeliveryRepository
{
    Task<Delivery?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Delivery?> GetByPackageRequestIdAsync(Guid packageRequestId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Delivery>> GetBySenderIdAsync(Guid senderId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Delivery>> GetByTravelerIdAsync(Guid travelerId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Delivery>> GetByTripIdAsync(Guid tripId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Delivery>> GetActiveDeliveriesAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Delivery delivery, CancellationToken cancellationToken = default);
    void Update(Delivery delivery);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
