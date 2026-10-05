using TagAlong.EventBus;
using TagAlong.Trip.Domain.Repositories;

namespace TagAlong.Trip.API.IntegrationEvents;

public record TripBookingsChangedIntegrationEvent(
    Guid TripId,
    int ActiveBookings,
    DateTime ChangedAt) : IntegrationEvent;

/// <summary>
/// Messaging publishes the number of LockedIn/InProgress conversations on a trip;
/// keep the trip's seat / package count in step so full trips drop out of search.
/// </summary>
public class TripBookingsChangedIntegrationEventHandler : IIntegrationEventHandler<TripBookingsChangedIntegrationEvent>
{
    private readonly ITripRepository _tripRepository;
    private readonly ILogger<TripBookingsChangedIntegrationEventHandler> _logger;

    public TripBookingsChangedIntegrationEventHandler(
        ITripRepository tripRepository,
        ILogger<TripBookingsChangedIntegrationEventHandler> logger)
    {
        _tripRepository = tripRepository;
        _logger = logger;
    }

    public async Task HandleAsync(TripBookingsChangedIntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        var trip = await _tripRepository.GetByIdAsync(@event.TripId, cancellationToken);
        if (trip == null) return;

        trip.SetActiveBookings(@event.ActiveBookings);
        _tripRepository.Update(trip);
        await _tripRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Trip {TripId} now has {Count} active booking(s)", @event.TripId, @event.ActiveBookings);
    }
}
