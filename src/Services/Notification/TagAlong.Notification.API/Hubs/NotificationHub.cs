using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using TagAlong.Notification.Domain.Entities;
using TagAlong.Notification.Domain.Repositories;

namespace TagAlong.Notification.API.Hubs;

public interface INotificationClient
{
    Task ReceiveNotification(NotificationDto notification);
    Task ReceiveNotifications(IEnumerable<NotificationDto> notifications);
    Task NotificationRead(Guid notificationId);
    Task AllNotificationsRead();
    Task UnreadCountChanged(int count);
}

public record NotificationDto(
    Guid Id,
    string Title,
    string Message,
    string Type,
    Guid? ReferenceId,
    string? ReferenceType,
    bool IsRead,
    string? Data,
    DateTime CreatedAt);

[Authorize]
public class NotificationHub : Hub<INotificationClient>
{
    private readonly IUserConnectionRepository _connectionRepository;
    private readonly INotificationRepository _notificationRepository;
    private readonly ILogger<NotificationHub> _logger;

    public NotificationHub(
        IUserConnectionRepository connectionRepository,
        INotificationRepository notificationRepository,
        ILogger<NotificationHub> logger)
    {
        _connectionRepository = connectionRepository;
        _notificationRepository = notificationRepository;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        if (userId.HasValue)
        {
            var connection = UserConnection.Create(userId.Value, Context.ConnectionId, GetDeviceType());
            await _connectionRepository.AddAsync(connection);
            await _connectionRepository.SaveChangesAsync();

            await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");

            _logger.LogInformation("User {UserId} connected with connection {ConnectionId}", userId, Context.ConnectionId);

            // Send unread notifications count
            var unreadCount = await _notificationRepository.GetUnreadCountAsync(userId.Value);
            await Clients.Caller.UnreadCountChanged(unreadCount);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();
        if (userId.HasValue)
        {
            await _connectionRepository.RemoveByConnectionIdAsync(Context.ConnectionId);
            await _connectionRepository.SaveChangesAsync();

            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user_{userId}");

            _logger.LogInformation("User {UserId} disconnected from connection {ConnectionId}", userId, Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task JoinGroup(string groupName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation("Connection {ConnectionId} joined group {GroupName}", Context.ConnectionId, groupName);
    }

    public async Task LeaveGroup(string groupName)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation("Connection {ConnectionId} left group {GroupName}", Context.ConnectionId, groupName);
    }

    public async Task GetUnreadNotifications()
    {
        var userId = GetUserId();
        if (userId.HasValue)
        {
            var notifications = await _notificationRepository.GetUnreadByUserIdAsync(userId.Value);
            var dtos = notifications.Select(MapToDto);
            await Clients.Caller.ReceiveNotifications(dtos);
        }
    }

    public async Task MarkAsRead(Guid notificationId)
    {
        var userId = GetUserId();
        if (userId.HasValue)
        {
            var notification = await _notificationRepository.GetByIdAsync(notificationId);
            if (notification != null && notification.UserId == userId.Value)
            {
                notification.MarkAsRead();
                _notificationRepository.Update(notification);
                await _notificationRepository.SaveChangesAsync();

                await Clients.Caller.NotificationRead(notificationId);

                var unreadCount = await _notificationRepository.GetUnreadCountAsync(userId.Value);
                await Clients.Caller.UnreadCountChanged(unreadCount);
            }
        }
    }

    public async Task MarkAllAsRead()
    {
        var userId = GetUserId();
        if (userId.HasValue)
        {
            await _notificationRepository.MarkAllAsReadAsync(userId.Value);
            await Clients.Caller.AllNotificationsRead();
            await Clients.Caller.UnreadCountChanged(0);
        }
    }

    private Guid? GetUserId()
    {
        var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }

    private string? GetDeviceType()
    {
        return Context.GetHttpContext()?.Request.Headers["X-Device-Type"].FirstOrDefault();
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
