using TagAlong.EventBus;
using TagAlong.User.Domain.Repositories;

namespace TagAlong.User.API.IntegrationEvents;

public record TravelerTripStateChangedIntegrationEvent(
    Guid TravelerId,
    int InProgressCount,
    DateTime ChangedAt) : IntegrationEvent;

/// <summary>
/// Messaging publishes this when a conversation trip starts, is delivered or is closed.
/// Keeps HasOngoingTrip in step so a traveler can't go offline mid-trip.
/// </summary>
public class TravelerTripStateChangedIntegrationEventHandler : IIntegrationEventHandler<TravelerTripStateChangedIntegrationEvent>
{
    private readonly IUserProfileRepository _userProfileRepository;
    private readonly ILogger<TravelerTripStateChangedIntegrationEventHandler> _logger;

    public TravelerTripStateChangedIntegrationEventHandler(
        IUserProfileRepository userProfileRepository,
        ILogger<TravelerTripStateChangedIntegrationEventHandler> logger)
    {
        _userProfileRepository = userProfileRepository;
        _logger = logger;
    }

    public async Task HandleAsync(TravelerTripStateChangedIntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        var profile = await _userProfileRepository.GetByAuthUserIdAsync(@event.TravelerId, cancellationToken);
        if (profile == null) return;

        var before = profile.HasOngoingTrip;
        profile.UpdateOngoingTripCount(@event.InProgressCount);
        if (profile.HasOngoingTrip == before) return;

        _logger.LogInformation(
            "Traveler {TravelerId} now has {Count} in-progress trip(s) — HasOngoingTrip={Flag}",
            @event.TravelerId, @event.InProgressCount, profile.HasOngoingTrip);

        _userProfileRepository.Update(profile);
        await _userProfileRepository.SaveChangesAsync(cancellationToken);
    }
}
