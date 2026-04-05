using Microsoft.AspNetCore.SignalR;
using TagAlong.Common.CQRS;
using TagAlong.Common.Results;
using TagAlong.Messaging.API.DTOs;
using TagAlong.Messaging.API.Hubs;
using TagAlong.Messaging.Domain.Entities;
using TagAlong.Messaging.Domain.Repositories;

namespace TagAlong.Messaging.API.Commands;

public record AcceptConversationCommand(Guid ConversationId, Guid TravelerId) : ICommand<ConversationDto>;

public class AcceptConversationCommandHandler : ICommandHandler<AcceptConversationCommand, ConversationDto>
{
    private readonly IConversationRepository _conversationRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly IHubContext<MessagingHub, IMessagingClient> _hubContext;

    public AcceptConversationCommandHandler(
        IConversationRepository conversationRepository,
        IMessageRepository messageRepository,
        IHubContext<MessagingHub, IMessagingClient> hubContext)
    {
        _conversationRepository = conversationRepository;
        _messageRepository = messageRepository;
        _hubContext = hubContext;
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

        return Result.Success(dto);
    }

    private static ConversationDto MapToDto(Conversation c) =>
        new(c.Id, c.PackageRequestId, c.SenderId, c.TravelerId, null, null, c.Status.ToString(), c.CreatedAt, c.UpdatedAt, null);
}
