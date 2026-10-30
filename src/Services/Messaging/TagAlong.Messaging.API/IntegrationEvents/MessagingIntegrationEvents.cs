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
/// <summary>A push about an on-demand ride (new request, counter-offer, matched, expired).</summary>
public record RidePushIntegrationEvent(
    Guid UserId,
    string Title,
    string Body,
    Guid ReferenceId,
    string ReferenceType) : IntegrationEvent;

/// <summary>Rings a phone for an in-app call ("incoming"), or stops it ringing ("ended").</summary>
public record CallPushIntegrationEvent(
    Guid UserId,
    string Kind,
    Guid CallId,
    Guid ConversationId,
    Guid CallerId,
    string CallerName) : IntegrationEvent;

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
