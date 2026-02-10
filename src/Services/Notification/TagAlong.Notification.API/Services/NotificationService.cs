using Microsoft.AspNetCore.SignalR;
using TagAlong.Notification.API.Hubs;
using TagAlong.Notification.Domain.Entities;
using TagAlong.Notification.Domain.Repositories;

namespace TagAlong.Notification.API.Services;

public interface INotificationService
{
    Task SendNotificationAsync(Guid userId, string title, string message, NotificationType type,
        Guid? referenceId = null, string? referenceType = null, string? data = null,
        CancellationToken cancellationToken = default);
    Task SendNotificationToMultipleUsersAsync(IEnumerable<Guid> userIds, string title, string message,
        NotificationType type, Guid? referenceId = null, string? referenceType = null, string? data = null,
        CancellationToken cancellationToken = default);
    Task BroadcastNotificationAsync(string title, string message, NotificationType type,
        CancellationToken cancellationToken = default);
}

public class NotificationService : INotificationService
{
    private readonly INotificationRepository _notificationRepository;
    private readonly IHubContext<NotificationHub, INotificationClient> _hubContext;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        INotificationRepository notificationRepository,
        IHubContext<NotificationHub, INotificationClient> hubContext,
        ILogger<NotificationService> logger)
    {
        _notificationRepository = notificationRepository;
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task SendNotificationAsync(
        Guid userId,
        string title,
        string message,
        NotificationType type,
        Guid? referenceId = null,
        string? referenceType = null,
        string? data = null,
        CancellationToken cancellationToken = default)
    {
        var notification = Domain.Entities.Notification.Create(
            userId, title, message, type, referenceId, referenceType, data);

        await _notificationRepository.AddAsync(notification, cancellationToken);
        await _notificationRepository.SaveChangesAsync(cancellationToken);

        var dto = MapToDto(notification);

        await _hubContext.Clients.Group($"user_{userId}").ReceiveNotification(dto);

        var unreadCount = await _notificationRepository.GetUnreadCountAsync(userId, cancellationToken);
        await _hubContext.Clients.Group($"user_{userId}").UnreadCountChanged(unreadCount);

        _logger.LogInformation("Sent notification {NotificationId} to user {UserId}", notification.Id, userId);
    }

    public async Task SendNotificationToMultipleUsersAsync(
        IEnumerable<Guid> userIds,
        string title,
        string message,
        NotificationType type,
        Guid? referenceId = null,
        string? referenceType = null,
        string? data = null,
        CancellationToken cancellationToken = default)
    {
        var userIdList = userIds.ToList();
        var notifications = userIdList.Select(userId =>
            Domain.Entities.Notification.Create(userId, title, message, type, referenceId, referenceType, data))
            .ToList();

        await _notificationRepository.AddRangeAsync(notifications, cancellationToken);
        await _notificationRepository.SaveChangesAsync(cancellationToken);

        foreach (var notification in notifications)
        {
            var dto = MapToDto(notification);
            await _hubContext.Clients.Group($"user_{notification.UserId}").ReceiveNotification(dto);

            var unreadCount = await _notificationRepository.GetUnreadCountAsync(notification.UserId, cancellationToken);
            await _hubContext.Clients.Group($"user_{notification.UserId}").UnreadCountChanged(unreadCount);
        }

        _logger.LogInformation("Sent notifications to {Count} users", userIdList.Count);
    }

    public async Task BroadcastNotificationAsync(
        string title,
        string message,
        NotificationType type,
        CancellationToken cancellationToken = default)
    {
        var dto = new NotificationDto(
            Guid.NewGuid(),
            title,
            message,
            type.ToString(),
            null,
            null,
            false,
            null,
            DateTime.UtcNow);

        await _hubContext.Clients.All.ReceiveNotification(dto);

        _logger.LogInformation("Broadcast notification to all users");
    }

    private static NotificationDto MapToDto(Domain.Entities.Notification notification)
    {
        return new NotificationDto(
            notification.Id,
            notification.Title,
            notification.Message,
            notification.Type.ToString(),
            notification.ReferenceId,
            notification.ReferenceType,
            notification.IsRead,
            notification.Data,
            notification.CreatedAt);
    }
}
