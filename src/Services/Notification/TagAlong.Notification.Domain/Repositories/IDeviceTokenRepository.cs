using TagAlong.Notification.Domain.Entities;

namespace TagAlong.Notification.Domain.Repositories;

public interface IDeviceTokenRepository
{
    Task<DeviceToken?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IEnumerable<string>> GetTokensByUserIdsAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken = default);
    Task AddAsync(DeviceToken token, CancellationToken cancellationToken = default);
    void Update(DeviceToken token);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
