using TagAlong.Notification.Domain.Entities;

namespace TagAlong.Notification.Domain.Repositories;

public interface IUserConnectionRepository
{
    Task<UserConnection?> GetByConnectionIdAsync(string connectionId, CancellationToken cancellationToken = default);
    Task<IEnumerable<UserConnection>> GetActiveConnectionsByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IEnumerable<string>> GetActiveConnectionIdsByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(UserConnection connection, CancellationToken cancellationToken = default);
    void Update(UserConnection connection);
    Task RemoveByConnectionIdAsync(string connectionId, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
