using TagAlong.Review.Domain.Entities;

namespace TagAlong.Review.Domain.Repositories;

public interface IReviewRepository
{
    Task<Entities.Review?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Entities.Review?> GetByDeliveryAndReviewerAsync(Guid deliveryId, Guid reviewerId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Entities.Review>> GetByRevieweeIdAsync(Guid revieweeId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<IEnumerable<Entities.Review>> GetByDeliveryIdAsync(Guid deliveryId, CancellationToken cancellationToken = default);
    Task<double> GetAverageRatingByRevieweeAsync(Guid revieweeId, CancellationToken cancellationToken = default);
    Task<int> GetReviewCountByRevieweeAsync(Guid revieweeId, CancellationToken cancellationToken = default);
    Task<Dictionary<int, int>> GetRatingDistributionAsync(Guid revieweeId, CancellationToken cancellationToken = default);
    Task AddAsync(Entities.Review review, CancellationToken cancellationToken = default);
    void Update(Entities.Review review);
    void Delete(Entities.Review review);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
