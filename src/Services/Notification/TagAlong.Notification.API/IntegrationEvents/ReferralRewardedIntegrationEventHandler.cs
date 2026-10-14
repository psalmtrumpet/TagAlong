using TagAlong.EventBus;
using TagAlong.Notification.API.Services;
using TagAlong.Notification.Domain.Entities;

namespace TagAlong.Notification.API.IntegrationEvents;

public record ReferralRewardedIntegrationEvent(
    Guid UserId,
    string Reward,
    decimal Amount,
    string? ReferredName) : IntegrationEvent;

/// <summary>Someone the user referred completed a ride.</summary>
public class ReferralRewardedIntegrationEventHandler : IIntegrationEventHandler<ReferralRewardedIntegrationEvent>
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<ReferralRewardedIntegrationEventHandler> _logger;

    public ReferralRewardedIntegrationEventHandler(
        INotificationService notificationService,
        ILogger<ReferralRewardedIntegrationEventHandler> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task HandleAsync(ReferralRewardedIntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Referral reward ({Reward}) for user {UserId}", @event.Reward, @event.UserId);
        var who = string.IsNullOrWhiteSpace(@event.ReferredName) ? "Someone you referred" : @event.ReferredName;
        var message = @event.Reward == "FreeTrip"
            ? $"{who} completed a ride. You've earned a ride free of the TagAlong fee."
            : $"{who} completed a ride. ₦{@event.Amount:N0} has been added to your wallet.";

        await _notificationService.SendNotificationAsync(
            @event.UserId,
            "You earned a referral reward",
            message,
            NotificationType.System,
            referenceType: "Referral",
            cancellationToken: cancellationToken);
    }
}
