using TagAlong.EventBus;
using TagAlong.User.Domain.Repositories;

namespace TagAlong.User.API.IntegrationEvents;

public record ConversationCompletedIntegrationEvent(
    Guid ConversationId,
    Guid? TripId,
    Guid SenderId,
    Guid TravelerId,
    bool IsDelivery,
    DateTime CompletedAt) : IntegrationEvent;

/// <summary>
/// A ride/delivery was marked delivered: count it for both participants
/// (CompletedTrips for passenger rides, CompletedDeliveries for packages).
/// </summary>
public class ConversationCompletedIntegrationEventHandler : IIntegrationEventHandler<ConversationCompletedIntegrationEvent>
{
    private readonly IUserProfileRepository _userProfileRepository;
    private readonly ILogger<ConversationCompletedIntegrationEventHandler> _logger;

    public ConversationCompletedIntegrationEventHandler(
        IUserProfileRepository userProfileRepository,
        ILogger<ConversationCompletedIntegrationEventHandler> logger)
    {
        _userProfileRepository = userProfileRepository;
        _logger = logger;
    }

    public async Task HandleAsync(ConversationCompletedIntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        foreach (var userId in new[] { @event.TravelerId, @event.SenderId })
        {
            var profile = await _userProfileRepository.GetByAuthUserIdAsync(userId, cancellationToken);
            if (profile == null) continue;

            if (@event.IsDelivery) profile.IncrementCompletedDeliveries();
            else profile.IncrementCompletedTrips();
            _userProfileRepository.Update(profile);
        }
        await _userProfileRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Counted completed {Kind} for conversation {ConversationId}",
            @event.IsDelivery ? "delivery" : "trip", @event.ConversationId);
    }
}
