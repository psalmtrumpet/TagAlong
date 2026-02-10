using Microsoft.EntityFrameworkCore;
using TagAlong.Review.Domain.Entities;
using TagAlong.Review.Domain.Repositories;
using TagAlong.Review.Infrastructure.Persistence;

namespace TagAlong.Review.Infrastructure.Repositories;

public class ReviewRepository : IReviewRepository
{
    private readonly ReviewDbContext _context;

    public ReviewRepository(ReviewDbContext context)
    {
        _context = context;
    }

    public async Task<Domain.Entities.Review?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Reviews.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<Domain.Entities.Review?> GetByDeliveryAndReviewerAsync(Guid deliveryId, Guid reviewerId, CancellationToken cancellationToken = default)
    {
        return await _context.Reviews
            .FirstOrDefaultAsync(r => r.DeliveryId == deliveryId && r.ReviewerId == reviewerId, cancellationToken);
    }

    public async Task<IEnumerable<Domain.Entities.Review>> GetByRevieweeIdAsync(Guid revieweeId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        return await _context.Reviews
            .Where(r => r.RevieweeId == revieweeId)
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Domain.Entities.Review>> GetByDeliveryIdAsync(Guid deliveryId, CancellationToken cancellationToken = default)
    {
        return await _context.Reviews
            .Where(r => r.DeliveryId == deliveryId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<double> GetAverageRatingByRevieweeAsync(Guid revieweeId, CancellationToken cancellationToken = default)
    {
        var hasReviews = await _context.Reviews.AnyAsync(r => r.RevieweeId == revieweeId, cancellationToken);
        if (!hasReviews) return 0;

        return await _context.Reviews
            .Where(r => r.RevieweeId == revieweeId)
            .AverageAsync(r => r.Rating, cancellationToken);
    }

    public async Task<int> GetReviewCountByRevieweeAsync(Guid revieweeId, CancellationToken cancellationToken = default)
    {
        return await _context.Reviews.CountAsync(r => r.RevieweeId == revieweeId, cancellationToken);
    }

    public async Task<Dictionary<int, int>> GetRatingDistributionAsync(Guid revieweeId, CancellationToken cancellationToken = default)
    {
        var distribution = await _context.Reviews
            .Where(r => r.RevieweeId == revieweeId)
            .GroupBy(r => r.Rating)
            .Select(g => new { Rating = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var result = new Dictionary<int, int>
        {
            { 1, 0 }, { 2, 0 }, { 3, 0 }, { 4, 0 }, { 5, 0 }
        };

        foreach (var item in distribution)
        {
            result[item.Rating] = item.Count;
        }

        return result;
    }

    public async Task AddAsync(Domain.Entities.Review review, CancellationToken cancellationToken = default)
    {
        await _context.Reviews.AddAsync(review, cancellationToken);
    }

    public void Update(Domain.Entities.Review review)
    {
        _context.Reviews.Update(review);
    }

    public void Delete(Domain.Entities.Review review)
    {
        _context.Reviews.Remove(review);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
