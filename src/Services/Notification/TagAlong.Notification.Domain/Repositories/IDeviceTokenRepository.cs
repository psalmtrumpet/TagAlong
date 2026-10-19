using TagAlong.Notification.Domain.Entities;

namespace TagAlong.Notification.Domain.Repositories;

public interface IDeviceTokenRepository
{
    Task<DeviceToken?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IEnumerable<string>> GetTokensByUserIdsAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken = default);
    Task AddAsync(DeviceToken token, CancellationToken cancellationToken = default);
    void Update(DeviceToken token);
    /// <summary>Unlinks a phone from every account except the one now signed in on it.</summary>
    Task RemoveTokenFromOtherUsersAsync(string token, Guid keepUserId, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
