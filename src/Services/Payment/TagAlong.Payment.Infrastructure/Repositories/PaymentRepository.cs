using Microsoft.EntityFrameworkCore;
using TagAlong.Payment.Domain.Entities;
using TagAlong.Payment.Domain.Repositories;
using TagAlong.Payment.Infrastructure.Persistence;

namespace TagAlong.Payment.Infrastructure.Repositories;

public class PaymentRepository : IPaymentRepository
{
    private readonly PaymentDbContext _context;

    public PaymentRepository(PaymentDbContext context)
    {
        _context = context;
    }

    public async Task<Domain.Entities.Payment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Payments.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<Domain.Entities.Payment?> GetByDeliveryIdAsync(Guid deliveryId, CancellationToken cancellationToken = default)
    {
        return await _context.Payments.FirstOrDefaultAsync(p => p.DeliveryId == deliveryId, cancellationToken);
    }

    public async Task<IEnumerable<Domain.Entities.Payment>> GetBySenderIdAsync(Guid senderId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        return await _context.Payments
            .Where(p => p.SenderId == senderId)
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Domain.Entities.Payment>> GetByTravelerIdAsync(Guid travelerId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        return await _context.Payments
            .Where(p => p.TravelerId == travelerId)
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Domain.Entities.Payment>> GetPendingPaymentsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Payments
            .Where(p => p.Status == PaymentStatus.Pending || p.Status == PaymentStatus.Processing)
            .OrderBy(p => p.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<decimal> GetTotalEarningsByTravelerAsync(Guid travelerId, CancellationToken cancellationToken = default)
    {
        return await _context.Payments
            .Where(p => p.TravelerId == travelerId && p.Status == PaymentStatus.Completed)
            .SumAsync(p => p.TravelerPayout, cancellationToken);
    }

    public async Task<decimal> GetTotalSpentBySenderAsync(Guid senderId, CancellationToken cancellationToken = default)
    {
        return await _context.Payments
            .Where(p => p.SenderId == senderId && p.Status == PaymentStatus.Completed)
            .SumAsync(p => p.Amount, cancellationToken);
    }

    public async Task AddAsync(Domain.Entities.Payment payment, CancellationToken cancellationToken = default)
    {
        await _context.Payments.AddAsync(payment, cancellationToken);
    }

    public void Update(Domain.Entities.Payment payment)
    {
        _context.Payments.Update(payment);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
