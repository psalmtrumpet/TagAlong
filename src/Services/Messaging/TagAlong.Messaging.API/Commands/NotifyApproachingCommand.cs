using TagAlong.Common.CQRS;
using TagAlong.Common.Results;
using TagAlong.EventBus;
using TagAlong.Messaging.API.IntegrationEvents;
using TagAlong.Messaging.Domain.Entities;
using TagAlong.Messaging.Domain.Repositories;

namespace TagAlong.Messaging.API.Commands;

/// <param name="Arrived">The driver is at the pickup (not just close).</param>
public record NotifyApproachingCommand(Guid ConversationId, Guid UserId, bool Arrived = false) : ICommand<bool>;

public class NotifyApproachingCommandHandler : ICommandHandler<NotifyApproachingCommand, bool>
{
    private readonly IConversationRepository _conversationRepository;
    private readonly IEventBus _eventBus;
    private readonly ILogger<NotifyApproachingCommandHandler> _logger;

    private readonly IMessageRepository _messages;
    private readonly Microsoft.AspNetCore.SignalR.IHubContext<TagAlong.Messaging.API.Hubs.MessagingHub, TagAlong.Messaging.API.Hubs.IMessagingClient> _hub;
    private readonly TagAlong.Messaging.API.Services.IUserLookupService _users;

    public NotifyApproachingCommandHandler(
        IConversationRepository conversationRepository,
        IEventBus eventBus,
        ILogger<NotifyApproachingCommandHandler> logger,
        IMessageRepository messages,
        Microsoft.AspNetCore.SignalR.IHubContext<TagAlong.Messaging.API.Hubs.MessagingHub, TagAlong.Messaging.API.Hubs.IMessagingClient> hub,
        TagAlong.Messaging.API.Services.IUserLookupService users)
    {
        _conversationRepository = conversationRepository;
        _eventBus = eventBus;
        _logger = logger;
        _messages = messages;
        _hub = hub;
        _users = users;
    }

    public async Task<Result<bool>> Handle(NotifyApproachingCommand request, CancellationToken cancellationToken)
    {
        var conversation = await _conversationRepository.GetByIdAsync(request.ConversationId, cancellationToken);
        if (conversation == null)
            return Result.Failure<bool>(Error.NotFound("Conversation not found"));

        // Only the driver (TravelerId) may call this
        if (conversation.TravelerId != request.UserId)
            return Result.Failure<bool>(Error.Unauthorized("Only the driver can send this notification"));

        // Only fire when driver is approaching (LockedIn) or ride has started (InProgress)
        if (conversation.Status != ConversationStatus.LockedIn && conversation.Status != ConversationStatus.InProgress)
            return Result.Success(false);

        if (request.Arrived)
        {
            var name = (await _users.GetDisplayNameAsync(request.UserId, cancellationToken))?.Split(' ')[0] ?? "Your driver";
            var where = conversation.MeetName ?? conversation.PickupAddress ?? "the pickup";
            // In the chat too, so it's there when they open it
            var msg = Message.CreateSystemMessage(request.ConversationId, $"{name} has arrived at {where}.");
            await _messages.AddAsync(msg, cancellationToken);
            await _messages.SaveChangesAsync(cancellationToken);
            var dto = new TagAlong.Messaging.API.DTOs.MessageDto(msg.Id, msg.ConversationId, msg.SenderId, msg.Content,
                msg.MessageType.ToString(), msg.ProposedPrice, msg.SentAt, msg.ReadAt);
            await _hub.Clients.Group($"conversation_{request.ConversationId}").ReceiveMessage(dto);
            await _hub.Clients.Group($"user_{conversation.SenderId}").ReceiveMessage(dto);

            await _eventBus.PublishAsync(new RidePushIntegrationEvent(
                conversation.SenderId,
                "Your driver has arrived",
                $"{name} is waiting for you at {where}. Check the plate before you get in.",
                request.ConversationId,
                "Conversation"), cancellationToken);
            _logger.LogInformation("Driver arrived for conversation {ConversationId}", request.ConversationId);
            return Result.Success(true);
        }

        await _eventBus.PublishAsync(new DriverApproachingIntegrationEvent(
            request.ConversationId,
            conversation.SenderId,
            string.Empty));

        _logger.LogInformation("Driver approaching event published for conversation {ConversationId}", request.ConversationId);
        return Result.Success(true);
    }
}
