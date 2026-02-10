using TagAlong.Notification.Domain.Entities;
using NotificationEntity = TagAlong.Notification.Domain.Entities.Notification;

namespace TagAlong.Notification.Domain.Repositories;

public interface INotificationRepository
{
    Task<NotificationEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<NotificationEntity>> GetByUserIdAsync(Guid userId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<IEnumerable<NotificationEntity>> GetUnreadByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(NotificationEntity notification, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<NotificationEntity> notifications, CancellationToken cancellationToken = default);
    void Update(NotificationEntity notification);
    Task MarkAllAsReadAsync(Guid userId, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
