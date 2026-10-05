using TagAlong.EventBus;
using TagAlong.User.Domain.Repositories;

namespace TagAlong.User.API.IntegrationEvents;

public record TripStatusChangedIntegrationEvent(
    Guid TripId,
    Guid TravelerId,
    string OldStatus,
    string NewStatus,
    DateTime ChangedAt) : IntegrationEvent;

/// <summary>
/// The go-offline lock (HasOngoingTrip) follows passengers/packages actually on board,
/// via TravelerTripStateChangedIntegrationEvent — starting a Trip alone doesn't lock,
/// so a trip with nobody on it can't strand the driver online.
/// Completing a Trip takes the driver offline unless someone is still on board.
/// </summary>
public class TripStatusChangedIntegrationEventHandler : IIntegrationEventHandler<TripStatusChangedIntegrationEvent>
{
    private readonly IUserProfileRepository _userProfileRepository;
    private readonly ILogger<TripStatusChangedIntegrationEventHandler> _logger;

    public TripStatusChangedIntegrationEventHandler(
        IUserProfileRepository userProfileRepository,
        ILogger<TripStatusChangedIntegrationEventHandler> logger)
    {
        _userProfileRepository = userProfileRepository;
        _logger = logger;
    }

    public async Task HandleAsync(TripStatusChangedIntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        if (@event.NewStatus != "Completed") return;

        var profile = await _userProfileRepository.GetByAuthUserIdAsync(@event.TravelerId, cancellationToken);
        if (profile == null || !profile.IsAvailable || profile.HasOngoingTrip) return;

        _logger.LogInformation("Trip {TripId} completed — taking traveler {TravelerId} offline", @event.TripId, @event.TravelerId);
        profile.SetUnavailable();
        _userProfileRepository.Update(profile);
        await _userProfileRepository.SaveChangesAsync(cancellationToken);
    }
}
