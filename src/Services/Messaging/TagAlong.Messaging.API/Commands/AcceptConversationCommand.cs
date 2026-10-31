using Microsoft.AspNetCore.SignalR;
using TagAlong.Common.CQRS;
using TagAlong.Common.Results;
using TagAlong.EventBus;
using TagAlong.Messaging.API.DTOs;
using TagAlong.Messaging.API.Hubs;
using TagAlong.Messaging.API.IntegrationEvents;
using TagAlong.Messaging.API.Services;
using TagAlong.Messaging.Domain.Entities;
using TagAlong.Messaging.Domain.Repositories;

namespace TagAlong.Messaging.API.Commands;

public record AcceptConversationCommand(Guid ConversationId, Guid TravelerId) : ICommand<ConversationDto>;

public class AcceptConversationCommandHandler : ICommandHandler<AcceptConversationCommand, ConversationDto>
{
    private readonly IConversationRepository _conversationRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly IHubContext<MessagingHub, IMessagingClient> _hubContext;
    private readonly IEventBus _eventBus;
    private readonly IUserLookupService _users;
    private readonly ILogger<AcceptConversationCommandHandler> _logger;

    public AcceptConversationCommandHandler(
        IConversationRepository conversationRepository,
        IMessageRepository messageRepository,
        IHubContext<MessagingHub, IMessagingClient> hubContext,
        IEventBus eventBus,
        IUserLookupService users,
        ILogger<AcceptConversationCommandHandler> logger)
    {
        _conversationRepository = conversationRepository;
        _messageRepository = messageRepository;
        _hubContext = hubContext;
        _eventBus = eventBus;
        _users = users;
        _logger = logger;
    }

    public async Task<Result<ConversationDto>> Handle(AcceptConversationCommand request, CancellationToken cancellationToken)
    {
        var conversation = await _conversationRepository.GetByIdAsync(request.ConversationId, cancellationToken);
        if (conversation == null)
            return Result.Failure<ConversationDto>(new Error("Conversation.NotFound", "Conversation not found"));

        if (conversation.TravelerId != request.TravelerId)
            return Result.Failure<ConversationDto>(new Error("Conversation.Forbidden", "Only the traveler can accept this request"));

        conversation.Accept();
        _conversationRepository.Update(conversation);

        // System message prompting the requester to suggest a price
        var systemMsg = Message.CreateSystemMessage(conversation.Id,
            "Request accepted! Please suggest how much you'd like to pay.");
        await _messageRepository.AddAsync(systemMsg, cancellationToken);

        await _conversationRepository.SaveChangesAsync(cancellationToken);

        var dto = MapToDto(conversation);

        // Notify the sender (requester) in real-time that their request was accepted
        await _hubContext.Clients.Group($"user_{conversation.SenderId}").ConversationUpdated(dto);

        // And with a push, in case their app is closed (tapping it opens the chat)
        try
        {
            var name = (await _users.GetDisplayNameAsync(conversation.TravelerId, cancellationToken))?.Split(' ')[0];
            await _eventBus.PublishAsync(new RidePushIntegrationEvent(
                conversation.SenderId,
                "Request accepted!",
                conversation.IsDelivery
                    ? $"{name ?? "Your carrier"} accepted your delivery request. Suggest a price to continue."
                    : $"{name ?? "Your driver"} accepted your ride request. Suggest a price to continue.",
                conversation.Id,
                "Conversation"), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Couldn't send the request-accepted push for {ConversationId}", conversation.Id);
        }

        return Result.Success(dto);
    }

    private static ConversationDto MapToDto(Conversation c) => ConversationDtoMapper.ToDto(c);
}
