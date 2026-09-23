using TagAlong.EventBus;
using TagAlong.Notification.API.Services;
using TagAlong.Notification.Domain.Entities;

namespace TagAlong.Notification.API.IntegrationEvents;

public record KycStatusChangedIntegrationEvent(
    Guid UserId,
    string Status,
    string? FailureReason) : IntegrationEvent;

public class KycStatusChangedIntegrationEventHandler : IIntegrationEventHandler<KycStatusChangedIntegrationEvent>
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<KycStatusChangedIntegrationEventHandler> _logger;

    public KycStatusChangedIntegrationEventHandler(
        INotificationService notificationService,
        ILogger<KycStatusChangedIntegrationEventHandler> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task HandleAsync(KycStatusChangedIntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("KYC status changed for user {UserId}: {Status}", @event.UserId, @event.Status);

        var isVerified = string.Equals(@event.Status, "Verified", StringComparison.OrdinalIgnoreCase);

        var title = isVerified ? "Identity Verified" : "Verification Failed";
        var message = isVerified
            ? "Your identity has been verified. You now have full access to TagAlong."
            : (@event.FailureReason ?? "Your identity verification failed. Please try again.");

        var type = isVerified ? NotificationType.IdentityVerified : NotificationType.IdentityFailed;

        await _notificationService.SendNotificationAsync(
            @event.UserId,
            title,
            message,
            type,
            cancellationToken: cancellationToken);
    }
}
