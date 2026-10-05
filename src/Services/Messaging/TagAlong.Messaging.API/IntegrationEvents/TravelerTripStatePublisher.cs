using TagAlong.EventBus;
using TagAlong.Messaging.Domain.Repositories;

namespace TagAlong.Messaging.API.IntegrationEvents;

public static class TravelerTripStatePublisher
{
    /// <summary>Call after saving a conversation trip-state change.</summary>
    public static async Task PublishAsync(
        IConversationRepository conversations,
        IEventBus eventBus,
        Guid travelerId,
        CancellationToken cancellationToken)
    {
        var count = await conversations.CountInProgressByTravelerIdAsync(travelerId, cancellationToken);
        await eventBus.PublishAsync(
            new TravelerTripStateChangedIntegrationEvent(travelerId, count, DateTime.UtcNow),
            cancellationToken);
    }
}
