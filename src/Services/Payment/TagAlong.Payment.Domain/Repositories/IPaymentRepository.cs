using TagAlong.Payment.Domain.Entities;

namespace TagAlong.Payment.Domain.Repositories;

public interface IPaymentRepository
{
    Task<Entities.Payment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Entities.Payment?> GetByDeliveryIdAsync(Guid deliveryId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Entities.Payment>> GetBySenderIdAsync(Guid senderId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<IEnumerable<Entities.Payment>> GetByTravelerIdAsync(Guid travelerId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<IEnumerable<Entities.Payment>> GetPendingPaymentsAsync(CancellationToken cancellationToken = default);
    Task<decimal> GetTotalEarningsByTravelerAsync(Guid travelerId, CancellationToken cancellationToken = default);
    Task<decimal> GetTotalSpentBySenderAsync(Guid senderId, CancellationToken cancellationToken = default);
    Task AddAsync(Entities.Payment payment, CancellationToken cancellationToken = default);
    void Update(Entities.Payment payment);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
