using Microsoft.EntityFrameworkCore;
using TagAlong.Notification.Domain.Entities;
using TagAlong.Notification.Domain.Repositories;
using TagAlong.Notification.Infrastructure.Persistence;

namespace TagAlong.Notification.Infrastructure.Repositories;

public class DeviceTokenRepository : IDeviceTokenRepository
{
    private readonly NotificationDbContext _context;

    public DeviceTokenRepository(NotificationDbContext context)
    {
        _context = context;
    }

    public Task<DeviceToken?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
        => _context.DeviceTokens.FirstOrDefaultAsync(t => t.UserId == userId, cancellationToken);

    public async Task<IEnumerable<string>> GetTokensByUserIdsAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken = default)
        => await _context.DeviceTokens
            .Where(t => userIds.Contains(t.UserId))
            .Select(t => t.Token)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(DeviceToken token, CancellationToken cancellationToken = default)
        => await _context.DeviceTokens.AddAsync(token, cancellationToken);

    public void Update(DeviceToken token)
        => _context.DeviceTokens.Update(token);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => _context.SaveChangesAsync(cancellationToken);
}
