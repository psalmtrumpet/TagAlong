using TagAlong.EventBus;
using TagAlong.Notification.API.Services;
using TagAlong.Notification.Domain.Entities;

namespace TagAlong.Notification.API.IntegrationEvents;

public record PaymentCompletedIntegrationEvent(
    Guid PaymentId,
    Guid DeliveryId,
    Guid SenderId,
    Guid TravelerId,
    decimal Amount,
    decimal PlatformFee,
    decimal TravelerPayout,
    DateTime CompletedAt) : IntegrationEvent;

public class PaymentCompletedIntegrationEventHandler : IIntegrationEventHandler<PaymentCompletedIntegrationEvent>
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<PaymentCompletedIntegrationEventHandler> _logger;

    public PaymentCompletedIntegrationEventHandler(
        INotificationService notificationService,
        ILogger<PaymentCompletedIntegrationEventHandler> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task HandleAsync(PaymentCompletedIntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Handling PaymentCompletedIntegrationEvent for payment {PaymentId}", @event.PaymentId);

        // Notify the sender
        await _notificationService.SendNotificationAsync(
            @event.SenderId,
            "Payment Completed",
            $"Your payment of {@event.Amount:C} has been processed successfully.",
            NotificationType.PaymentReceived,
            @event.PaymentId,
            "Payment",
            System.Text.Json.JsonSerializer.Serialize(new { @event.Amount, @event.DeliveryId }),
            cancellationToken);

        // Notify the traveler
        await _notificationService.SendNotificationAsync(
            @event.TravelerId,
            "Payment Received!",
            $"You have received {@event.TravelerPayout:C} for your delivery.",
            NotificationType.PaymentReceived,
            @event.PaymentId,
            "Payment",
            System.Text.Json.JsonSerializer.Serialize(new { @event.TravelerPayout, @event.DeliveryId }),
            cancellationToken);
    }
}
