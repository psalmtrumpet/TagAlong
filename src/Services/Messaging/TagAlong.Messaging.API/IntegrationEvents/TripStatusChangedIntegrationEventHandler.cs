using TagAlong.EventBus;
using TagAlong.Messaging.Domain.Entities;
using TagAlong.Messaging.Domain.Repositories;

namespace TagAlong.Messaging.API.IntegrationEvents;

public record TripStatusChangedIntegrationEvent(
    Guid TripId,
    Guid TravelerId,
    string OldStatus,
    string NewStatus,
    DateTime ChangedAt) : IntegrationEvent;

public class TripStatusChangedIntegrationEventHandler
    : IIntegrationEventHandler<TripStatusChangedIntegrationEvent>
{
    private readonly IConversationRepository _conversationRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly ILogger<TripStatusChangedIntegrationEventHandler> _logger;

    public TripStatusChangedIntegrationEventHandler(
        IConversationRepository conversationRepository,
        IMessageRepository messageRepository,
        ILogger<TripStatusChangedIntegrationEventHandler> logger)
    {
        _conversationRepository = conversationRepository;
        _messageRepository = messageRepository;
        _logger = logger;
    }

    public async Task HandleAsync(TripStatusChangedIntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        var newStatus = @event.NewStatus.ToLower();
        if (newStatus != "completed" && newStatus != "cancelled")
            return;

        _logger.LogInformation(
            "Trip {TripId} ended with status {Status} — closing its unstarted bookings",
            @event.TripId, @event.NewStatus);

        var conversations = await _conversationRepository.GetUnstartedByTripIdAsync(@event.TripId, cancellationToken);

        var reason = newStatus == "completed"
            ? "This trip has ended. You can view the chat history."
            : "This trip was cancelled. You can view the chat history.";

        foreach (var conversation in conversations)
        {
            conversation.Close();
            _conversationRepository.Update(conversation);

            var systemMsg = Message.CreateSystemMessage(conversation.Id, reason);
            await _messageRepository.AddAsync(systemMsg, cancellationToken);
        }

        await _conversationRepository.SaveChangesAsync(cancellationToken);
    }
}
