using TagAlong.EventBus;
using TagAlong.Notification.API.Services;
using TagAlong.Notification.Domain.Entities;

namespace TagAlong.Notification.API.IntegrationEvents;

public record DeliveryMatchedIntegrationEvent(
    Guid DeliveryId,
    Guid PackageRequestId,
    Guid TripId,
    Guid SenderId,
    Guid TravelerId,
    decimal AgreedPrice,
    DateTime MatchedAt) : IntegrationEvent;

public class DeliveryMatchedIntegrationEventHandler : IIntegrationEventHandler<DeliveryMatchedIntegrationEvent>
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<DeliveryMatchedIntegrationEventHandler> _logger;

    public DeliveryMatchedIntegrationEventHandler(
        INotificationService notificationService,
        ILogger<DeliveryMatchedIntegrationEventHandler> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task HandleAsync(DeliveryMatchedIntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Handling DeliveryMatchedIntegrationEvent for delivery {DeliveryId}", @event.DeliveryId);

        // Notify the sender
        await _notificationService.SendNotificationAsync(
            @event.SenderId,
            "Package Matched!",
            $"Your package has been matched with a traveler. Agreed price: {@event.AgreedPrice:C}",
            NotificationType.PackageMatched,
            @event.DeliveryId,
            "Delivery",
            System.Text.Json.JsonSerializer.Serialize(new { @event.TripId, @event.AgreedPrice }),
            cancellationToken);

        // Notify the traveler
        await _notificationService.SendNotificationAsync(
            @event.TravelerId,
            "New Delivery Request",
            $"You have a new delivery request. Amount: {@event.AgreedPrice:C}",
            NotificationType.PackageMatched,
            @event.DeliveryId,
            "Delivery",
            System.Text.Json.JsonSerializer.Serialize(new { @event.PackageRequestId, @event.AgreedPrice }),
            cancellationToken);
    }
}
