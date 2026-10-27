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
        ILogger<TripStatusChangedIntegrationEventHandler> logger,
        MediatR.IMediator mediator,
        Microsoft.AspNetCore.SignalR.IHubContext<TagAlong.Messaging.API.Hubs.MessagingHub, TagAlong.Messaging.API.Hubs.IMessagingClient> hub)
    {
        _conversationRepository = conversationRepository;
        _messageRepository = messageRepository;
        _logger = logger;
        _mediator = mediator;
        _hub = hub;
    }

    private readonly MediatR.IMediator _mediator;
    private readonly Microsoft.AspNetCore.SignalR.IHubContext<TagAlong.Messaging.API.Hubs.MessagingHub, TagAlong.Messaging.API.Hubs.IMessagingClient> _hub;

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

        // Both sides see the booking close straight away
        foreach (var conversation in conversations)
        {
            var dto = TagAlong.Messaging.API.Commands.ConversationDtoMapper.ToDto(conversation);
            await _hub.Clients.Group($"user_{conversation.SenderId}").ConversationUpdated(dto);
            await _hub.Clients.Group($"user_{conversation.TravelerId}").ConversationUpdated(dto);
        }

        // Journey over: anyone still on board has arrived — complete their ride
        // exactly like a drop-off (fee, earnings, referral, rating prompt).
        if (newStatus == "completed")
        {
            foreach (var onBoard in await _conversationRepository.GetInProgressByTripIdAsync(@event.TripId, cancellationToken))
            {
                var result = await _mediator.Send(
                    new TagAlong.Messaging.API.Commands.MarkDeliveredCommand(onBoard.Id, onBoard.TravelerId, BySystem: true), cancellationToken);
                if (result.IsFailure)
                    _logger.LogWarning("Couldn't complete ride {ConversationId} when its trip ended: {Error}", onBoard.Id, result.Error.Message);
                else
                    _logger.LogInformation("Completed on-board ride {ConversationId} because trip {TripId} ended", onBoard.Id, @event.TripId);
            }
        }
    }
}
