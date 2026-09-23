using FirebaseAdmin.Messaging;
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
    private readonly IDeviceTokenRepository _deviceTokenRepository;
    private readonly IHubContext<NotificationHub, INotificationClient> _hubContext;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        INotificationRepository notificationRepository,
        IDeviceTokenRepository deviceTokenRepository,
        IHubContext<NotificationHub, INotificationClient> hubContext,
        ILogger<NotificationService> logger)
    {
        _notificationRepository = notificationRepository;
        _deviceTokenRepository = deviceTokenRepository;
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

        // Real-time push via SignalR (when app is open)
        await _hubContext.Clients.Group($"user_{userId}").ReceiveNotification(dto);

        var unreadCount = await _notificationRepository.GetUnreadCountAsync(userId, cancellationToken);
        await _hubContext.Clients.Group($"user_{userId}").UnreadCountChanged(unreadCount);

        // OS-level push via FCM (when app is closed / backgrounded)
        var deviceToken = await _deviceTokenRepository.GetByUserIdAsync(userId, cancellationToken);
        if (deviceToken != null)
            await SendFcmAsync(deviceToken.Token, title, message, type, referenceId, referenceType, cancellationToken);

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

        var fcmTokens = (await _deviceTokenRepository.GetTokensByUserIdsAsync(userIdList, cancellationToken)).ToList();

        foreach (var notification in notifications)
        {
            var dto = MapToDto(notification);
            await _hubContext.Clients.Group($"user_{notification.UserId}").ReceiveNotification(dto);

            var unreadCount = await _notificationRepository.GetUnreadCountAsync(notification.UserId, cancellationToken);
            await _hubContext.Clients.Group($"user_{notification.UserId}").UnreadCountChanged(unreadCount);
        }

        if (fcmTokens.Count > 0)
            await SendFcmMulticastAsync(fcmTokens, title, message, type, referenceId, referenceType, cancellationToken);

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

    private async Task SendFcmAsync(string token, string title, string body, NotificationType type,
        Guid? referenceId, string? referenceType, CancellationToken cancellationToken)
    {
        try
        {
            var message = new Message
            {
                Token = token,
                Notification = new FirebaseAdmin.Messaging.Notification { Title = title, Body = body },
                Data = new Dictionary<string, string>
                {
                    ["type"] = type.ToString(),
                    ["referenceId"] = referenceId?.ToString() ?? "",
                    ["referenceType"] = referenceType ?? ""
                },
                Android = new AndroidConfig
                {
                    Priority = Priority.High,
                    Notification = new AndroidNotification { Sound = "default" }
                },
                Apns = new ApnsConfig
                {
                    Aps = new Aps { Sound = "default", Badge = 1 }
                }
            };
            await FirebaseMessaging.DefaultInstance.SendAsync(message, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "FCM send failed for token {Token}", token[..Math.Min(10, token.Length)]);
        }
    }

    private async Task SendFcmMulticastAsync(List<string> tokens, string title, string body, NotificationType type,
        Guid? referenceId, string? referenceType, CancellationToken cancellationToken)
    {
        try
        {
            var message = new MulticastMessage
            {
                Tokens = tokens,
                Notification = new FirebaseAdmin.Messaging.Notification { Title = title, Body = body },
                Data = new Dictionary<string, string>
                {
                    ["type"] = type.ToString(),
                    ["referenceId"] = referenceId?.ToString() ?? "",
                    ["referenceType"] = referenceType ?? ""
                },
                Android = new AndroidConfig
                {
                    Priority = Priority.High,
                    Notification = new AndroidNotification { Sound = "default" }
                }
            };
            await FirebaseMessaging.DefaultInstance.SendEachForMulticastAsync(message, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "FCM multicast send failed");
        }
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
