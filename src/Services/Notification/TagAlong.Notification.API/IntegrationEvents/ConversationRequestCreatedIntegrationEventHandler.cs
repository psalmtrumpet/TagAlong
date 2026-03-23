using TagAlong.EventBus;
using TagAlong.Notification.API.Services;
using TagAlong.Notification.Domain.Entities;

namespace TagAlong.Notification.API.IntegrationEvents;

public record ConversationRequestCreatedIntegrationEvent(
    Guid ConversationId,
    Guid SenderId,
    Guid TravelerId,
    string InitialMessage,
    DateTime CreatedAt) : IntegrationEvent;

public class ConversationRequestCreatedIntegrationEventHandler
    : IIntegrationEventHandler<ConversationRequestCreatedIntegrationEvent>
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<ConversationRequestCreatedIntegrationEventHandler> _logger;

    public ConversationRequestCreatedIntegrationEventHandler(
        INotificationService notificationService,
        ILogger<ConversationRequestCreatedIntegrationEventHandler> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task HandleAsync(ConversationRequestCreatedIntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("New conversation request for traveler {TravelerId}", @event.TravelerId);

        // Notify the traveler
        await _notificationService.SendNotificationAsync(
            @event.TravelerId,
            "New Trip Request",
            @event.InitialMessage,
            NotificationType.NewMessage,
            @event.ConversationId,
            "Conversation",
            null,
            cancellationToken);
    }
}
