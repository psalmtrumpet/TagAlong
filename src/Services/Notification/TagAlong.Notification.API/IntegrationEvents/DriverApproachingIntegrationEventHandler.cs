using TagAlong.EventBus;
using TagAlong.Notification.API.Services;
using TagAlong.Notification.Domain.Entities;

namespace TagAlong.Notification.API.IntegrationEvents;

public record DriverApproachingIntegrationEvent(
    Guid ConversationId,
    Guid PassengerId,
    string DriverName) : IntegrationEvent;

public class DriverApproachingIntegrationEventHandler
    : IIntegrationEventHandler<DriverApproachingIntegrationEvent>
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<DriverApproachingIntegrationEventHandler> _logger;

    public DriverApproachingIntegrationEventHandler(
        INotificationService notificationService,
        ILogger<DriverApproachingIntegrationEventHandler> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task HandleAsync(DriverApproachingIntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Driver approaching passenger {PassengerId} for conversation {ConversationId}",
            @event.PassengerId, @event.ConversationId);

        await _notificationService.SendNotificationAsync(
            @event.PassengerId,
            "Driver is almost here!",
            "Your driver is close by — get ready to board.",
            NotificationType.System,
            @event.ConversationId,
            "Conversation",
            null,
            cancellationToken);
    }
}
