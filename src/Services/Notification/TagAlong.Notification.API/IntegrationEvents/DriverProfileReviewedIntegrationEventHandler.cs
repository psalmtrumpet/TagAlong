using TagAlong.EventBus;
using TagAlong.Notification.API.Services;
using TagAlong.Notification.Domain.Entities;

namespace TagAlong.Notification.API.IntegrationEvents;

public record DriverProfileReviewedIntegrationEvent(
    Guid UserId,
    string Status,
    string? Reason) : IntegrationEvent;

/// <summary>Tells a driver their licence and vehicle were approved or rejected.</summary>
public class DriverProfileReviewedIntegrationEventHandler : IIntegrationEventHandler<DriverProfileReviewedIntegrationEvent>
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<DriverProfileReviewedIntegrationEventHandler> _logger;

    public DriverProfileReviewedIntegrationEventHandler(
        INotificationService notificationService,
        ILogger<DriverProfileReviewedIntegrationEventHandler> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task HandleAsync(DriverProfileReviewedIntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Driver profile {Status} for user {UserId}", @event.Status, @event.UserId);

        var approved = string.Equals(@event.Status, "Approved", StringComparison.OrdinalIgnoreCase);
        var title = approved ? "You're approved to drive" : "Driver details not approved";
        var message = approved
            ? "Your licence and vehicle have been approved. You can now offer rides and deliveries."
            : "We couldn't approve your licence and vehicle. Check your email for what to fix, then upload clear, correct photos again.";

        await _notificationService.SendNotificationAsync(
            @event.UserId,
            title,
            message,
            NotificationType.System,
            referenceType: "DriverProfile",
            cancellationToken: cancellationToken);
    }
}
