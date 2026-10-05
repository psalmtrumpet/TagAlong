using TagAlong.EventBus;
using TagAlong.Messaging.Domain.Entities;
using TagAlong.Messaging.Domain.Repositories;

namespace TagAlong.Messaging.API.IntegrationEvents;

/// <summary>
/// Publishes the cross-service effects of conversation trip-state changes.
/// Call after the change has been saved.
/// </summary>
public static class ConversationLifecyclePublisher
{
    /// <summary>User service: lock go-offline while the traveler has passengers/packages on board.</summary>
    public static async Task TravelerTripStateAsync(
        IConversationRepository conversations, IEventBus eventBus, Guid travelerId, CancellationToken cancellationToken)
    {
        var count = await conversations.CountInProgressByTravelerIdAsync(travelerId, cancellationToken);
        await eventBus.PublishAsync(
            new TravelerTripStateChangedIntegrationEvent(travelerId, count, DateTime.UtcNow),
            cancellationToken);
    }

    /// <summary>Trip service: seats / package slots currently booked on the trip.</summary>
    public static async Task TripBookingsAsync(
        IConversationRepository conversations, IEventBus eventBus, Conversation conversation, CancellationToken cancellationToken)
    {
        if (conversation.TripId is not { } tripId) return;
        var count = await conversations.CountActiveBookingsByTripIdAsync(tripId, cancellationToken);
        await eventBus.PublishAsync(
            new TripBookingsChangedIntegrationEvent(tripId, count, DateTime.UtcNow),
            cancellationToken);
    }

    /// <summary>User service: count a completed ride/delivery for both participants.</summary>
    public static Task CompletedAsync(IEventBus eventBus, Conversation conversation, CancellationToken cancellationToken) =>
        eventBus.PublishAsync(
            new ConversationCompletedIntegrationEvent(
                conversation.Id,
                conversation.TripId,
                conversation.SenderId,
                conversation.TravelerId,
                IsDelivery: conversation.IsDelivery,
                DateTime.UtcNow),
            cancellationToken);
}
