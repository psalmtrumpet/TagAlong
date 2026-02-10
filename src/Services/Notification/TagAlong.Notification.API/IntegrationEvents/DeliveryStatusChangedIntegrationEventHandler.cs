using TagAlong.EventBus;
using TagAlong.Notification.API.Services;
using TagAlong.Notification.Domain.Entities;

namespace TagAlong.Notification.API.IntegrationEvents;

public record DeliveryStatusChangedIntegrationEvent(
    Guid DeliveryId,
    Guid PackageRequestId,
    Guid SenderId,
    Guid TravelerId,
    string OldStatus,
    string NewStatus,
    DateTime ChangedAt) : IntegrationEvent;

public class DeliveryStatusChangedIntegrationEventHandler : IIntegrationEventHandler<DeliveryStatusChangedIntegrationEvent>
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<DeliveryStatusChangedIntegrationEventHandler> _logger;

    public DeliveryStatusChangedIntegrationEventHandler(
        INotificationService notificationService,
        ILogger<DeliveryStatusChangedIntegrationEventHandler> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task HandleAsync(DeliveryStatusChangedIntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Handling DeliveryStatusChangedIntegrationEvent for delivery {DeliveryId}: {OldStatus} -> {NewStatus}",
            @event.DeliveryId, @event.OldStatus, @event.NewStatus);

        var (title, message, type) = GetNotificationContent(@event.NewStatus);

        // Notify the sender
        await _notificationService.SendNotificationAsync(
            @event.SenderId,
            title,
            message,
            type,
            @event.DeliveryId,
            "Delivery",
            System.Text.Json.JsonSerializer.Serialize(new { @event.NewStatus }),
            cancellationToken);

        // For certain statuses, also notify the traveler
        if (@event.NewStatus is "Cancelled")
        {
            await _notificationService.SendNotificationAsync(
                @event.TravelerId,
                "Delivery Cancelled",
                "A delivery has been cancelled.",
                NotificationType.DeliveryStatusChanged,
                @event.DeliveryId,
                "Delivery",
                null,
                cancellationToken);
        }
    }

    private static (string title, string message, NotificationType type) GetNotificationContent(string status)
    {
        return status switch
        {
            "Accepted" => ("Delivery Accepted", "The traveler has accepted your delivery request.", NotificationType.DeliveryStatusChanged),
            "Rejected" => ("Delivery Rejected", "The traveler has rejected your delivery request.", NotificationType.DeliveryStatusChanged),
            "PickedUp" => ("Package Picked Up", "Your package has been picked up by the traveler.", NotificationType.DeliveryPickedUp),
            "InTransit" => ("Package In Transit", "Your package is on its way!", NotificationType.DeliveryStatusChanged),
            "Delivered" => ("Package Delivered!", "Your package has been delivered successfully.", NotificationType.DeliveryCompleted),
            "Cancelled" => ("Delivery Cancelled", "The delivery has been cancelled.", NotificationType.DeliveryStatusChanged),
            _ => ("Delivery Update", $"Delivery status changed to {status}.", NotificationType.DeliveryStatusChanged)
        };
    }
}
