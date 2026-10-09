using TagAlong.EventBus;

namespace TagAlong.Messaging.API.IntegrationEvents;

public record NegotiationMessageSentIntegrationEvent(
    Guid ConversationId,
    Guid? PackageRequestId,
    Guid SenderId,
    Guid RecipientId,
    string MessageType,
    decimal? ProposedPrice,
    DateTime SentAt) : IntegrationEvent;

public record ConversationRequestCreatedIntegrationEvent(
    Guid ConversationId,
    Guid SenderId,
    Guid TravelerId,
    string InitialMessage,
    DateTime CreatedAt) : IntegrationEvent;

public record PriceAcceptedIntegrationEvent(
    Guid ConversationId,
    Guid? PackageRequestId,
    Guid SenderId,
    Guid TravelerId,
    decimal AcceptedPrice,
    DateTime AcceptedAt) : IntegrationEvent;

/// <summary>The car is close to a riding passenger's drop-off — "your stop is next".</summary>
public record PassengerStopNextIntegrationEvent(
    Guid ConversationId,
    Guid PassengerId,
    string StopName) : IntegrationEvent;

public record DriverApproachingIntegrationEvent(
    Guid ConversationId,
    Guid PassengerId,
    string DriverName) : IntegrationEvent;

/// <summary>
/// Raised whenever a traveler's number of in-progress trips may have changed.
/// The User service uses it to stop the traveler going offline mid-trip.
/// </summary>
public record TravelerTripStateChangedIntegrationEvent(
    Guid TravelerId,
    int InProgressCount,
    DateTime ChangedAt) : IntegrationEvent;

/// <summary>Number of LockedIn/InProgress bookings on a trip (seats or package slots taken).</summary>
public record TripBookingsChangedIntegrationEvent(
    Guid TripId,
    int ActiveBookings,
    DateTime ChangedAt) : IntegrationEvent;

/// <summary>A ride or delivery was completed (marked delivered / dropped off).</summary>
public record ConversationCompletedIntegrationEvent(
    Guid ConversationId,
    Guid? TripId,
    Guid SenderId,
    Guid TravelerId,
    bool IsDelivery,
    DateTime CompletedAt) : IntegrationEvent;
