using TagAlong.EventBus;
using TagAlong.Notification.API.Services;
using TagAlong.Notification.Domain.Entities;

namespace TagAlong.Notification.API.IntegrationEvents;

public record PassengerStopNextIntegrationEvent(
    Guid ConversationId,
    Guid PassengerId,
    string StopName) : IntegrationEvent;

/// <summary>The car is about to reach a riding passenger's stop.</summary>
public class PassengerStopNextIntegrationEventHandler : IIntegrationEventHandler<PassengerStopNextIntegrationEvent>
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<PassengerStopNextIntegrationEventHandler> _logger;

    public PassengerStopNextIntegrationEventHandler(
        INotificationService notificationService,
        ILogger<PassengerStopNextIntegrationEventHandler> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task HandleAsync(PassengerStopNextIntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Next stop for passenger {PassengerId} ({ConversationId})", @event.PassengerId, @event.ConversationId);
        await _notificationService.SendNotificationAsync(
            @event.PassengerId,
            "Your stop is next",
            $"{@event.StopName} is coming up — get ready to get off.",
            NotificationType.System,
            @event.ConversationId,
            "Conversation",
            null,
            cancellationToken);
    }
}
