using Microsoft.EntityFrameworkCore;
using TagAlong.Notification.Domain.Entities;
using TagAlong.Notification.Domain.Repositories;
using TagAlong.Notification.Infrastructure.Persistence;

namespace TagAlong.Notification.Infrastructure.Repositories;

public class UserConnectionRepository : IUserConnectionRepository
{
    private readonly NotificationDbContext _context;

    public UserConnectionRepository(NotificationDbContext context)
    {
        _context = context;
    }

    public async Task<UserConnection?> GetByConnectionIdAsync(string connectionId, CancellationToken cancellationToken = default)
    {
        return await _context.UserConnections
            .FirstOrDefaultAsync(c => c.ConnectionId == connectionId, cancellationToken);
    }

    public async Task<IEnumerable<UserConnection>> GetActiveConnectionsByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _context.UserConnections
            .Where(c => c.UserId == userId && c.IsActive)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<string>> GetActiveConnectionIdsByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _context.UserConnections
            .Where(c => c.UserId == userId && c.IsActive)
            .Select(c => c.ConnectionId)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(UserConnection connection, CancellationToken cancellationToken = default)
    {
        await _context.UserConnections.AddAsync(connection, cancellationToken);
    }

    public void Update(UserConnection connection)
    {
        _context.UserConnections.Update(connection);
    }

    public async Task RemoveByConnectionIdAsync(string connectionId, CancellationToken cancellationToken = default)
    {
        var connection = await GetByConnectionIdAsync(connectionId, cancellationToken);
        if (connection != null)
        {
            connection.Disconnect();
        }
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
