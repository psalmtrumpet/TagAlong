using TagAlong.EventBus;
using TagAlong.User.Domain.Repositories;

namespace TagAlong.User.API.IntegrationEvents;

public record TripStatusChangedIntegrationEvent(
    Guid TripId,
    Guid TravelerId,
    string OldStatus,
    string NewStatus,
    DateTime ChangedAt) : IntegrationEvent;

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
        var profile = await _userProfileRepository.GetByAuthUserIdAsync(@event.TravelerId, cancellationToken);
        if (profile == null) return;

        if (@event.NewStatus == "InProgress")
        {
            _logger.LogInformation("Trip {TripId} started for traveler {TravelerId} — locking availability on", @event.TripId, @event.TravelerId);
            profile.SetTripStarted();
            _userProfileRepository.Update(profile);
            await _userProfileRepository.SaveChangesAsync(cancellationToken);
        }
        else if (@event.NewStatus is "Completed" or "Cancelled")
        {
            _logger.LogInformation("Trip {TripId} ended ({Status}) for traveler {TravelerId} — clearing availability", @event.TripId, @event.NewStatus, @event.TravelerId);
            profile.SetTripEnded();
            _userProfileRepository.Update(profile);
            await _userProfileRepository.SaveChangesAsync(cancellationToken);
        }
    }
}
