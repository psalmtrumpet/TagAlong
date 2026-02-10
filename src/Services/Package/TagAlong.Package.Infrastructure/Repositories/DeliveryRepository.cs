using Microsoft.EntityFrameworkCore;
using TagAlong.Package.Domain.Entities;
using TagAlong.Package.Domain.Repositories;
using TagAlong.Package.Infrastructure.Persistence;

namespace TagAlong.Package.Infrastructure.Repositories;

public class DeliveryRepository : IDeliveryRepository
{
    private readonly PackageDbContext _context;

    public DeliveryRepository(PackageDbContext context)
    {
        _context = context;
    }

    public async Task<Delivery?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Deliveries.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
    }

    public async Task<Delivery?> GetByPackageRequestIdAsync(Guid packageRequestId, CancellationToken cancellationToken = default)
    {
        return await _context.Deliveries
            .Where(d => d.PackageRequestId == packageRequestId && d.Status != DeliveryStatus.Rejected && d.Status != DeliveryStatus.Cancelled)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IEnumerable<Delivery>> GetBySenderIdAsync(Guid senderId, CancellationToken cancellationToken = default)
    {
        return await _context.Deliveries
            .Where(d => d.SenderId == senderId)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Delivery>> GetByTravelerIdAsync(Guid travelerId, CancellationToken cancellationToken = default)
    {
        return await _context.Deliveries
            .Where(d => d.TravelerId == travelerId)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Delivery>> GetByTripIdAsync(Guid tripId, CancellationToken cancellationToken = default)
    {
        return await _context.Deliveries
            .Where(d => d.TripId == tripId)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Delivery>> GetActiveDeliveriesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Deliveries
            .Where(d => d.Status != DeliveryStatus.Delivered &&
                       d.Status != DeliveryStatus.Cancelled &&
                       d.Status != DeliveryStatus.Rejected)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Delivery delivery, CancellationToken cancellationToken = default)
    {
        await _context.Deliveries.AddAsync(delivery, cancellationToken);
    }

    public void Update(Delivery delivery)
    {
        _context.Deliveries.Update(delivery);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
