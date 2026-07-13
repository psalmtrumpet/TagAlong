using Microsoft.AspNetCore.SignalR;
using TagAlong.Common.CQRS;
using TagAlong.Common.Results;
using TagAlong.Messaging.API.DTOs;
using TagAlong.Messaging.API.Hubs;
using TagAlong.Messaging.Domain.Entities;
using TagAlong.Messaging.Domain.Repositories;

namespace TagAlong.Messaging.API.Commands;

public record CloseConversationCommand(Guid ConversationId, Guid UserId) : ICommand<ConversationDto>;

public class CloseConversationCommandHandler : ICommandHandler<CloseConversationCommand, ConversationDto>
{
    private readonly IConversationRepository _conversationRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly IHubContext<MessagingHub, IMessagingClient> _hubContext;

    public CloseConversationCommandHandler(
        IConversationRepository conversationRepository,
        IMessageRepository messageRepository,
        IHubContext<MessagingHub, IMessagingClient> hubContext)
    {
        _conversationRepository = conversationRepository;
        _messageRepository = messageRepository;
        _hubContext = hubContext;
    }

    public async Task<Result<ConversationDto>> Handle(CloseConversationCommand request, CancellationToken cancellationToken)
    {
        var conversation = await _conversationRepository.GetByIdAsync(request.ConversationId, cancellationToken);
        if (conversation == null)
            return Result.Failure<ConversationDto>(new Error("Conversation.NotFound", "Conversation not found"));

        if (!conversation.IsParticipant(request.UserId))
            return Result.Failure<ConversationDto>(new Error("Conversation.Forbidden", "Not a participant in this conversation"));

        conversation.Close();
        _conversationRepository.Update(conversation);

        var systemMsg = Message.CreateSystemMessage(conversation.Id, "The chat has been ended.");
        await _messageRepository.AddAsync(systemMsg, cancellationToken);

        await _conversationRepository.SaveChangesAsync(cancellationToken);

        var dto = MapToDto(conversation);

        // Notify the other party
        var otherUserId = conversation.GetOtherParticipant(request.UserId);
        await _hubContext.Clients.Group($"user_{otherUserId}").ConversationUpdated(dto);
        await _hubContext.Clients.Group($"conversation_{request.ConversationId}").ConversationUpdated(dto);

        return Result.Success(dto);
    }

    private static ConversationDto MapToDto(Conversation c) =>
        new(c.Id, c.PackageRequestId, c.SenderId, c.TravelerId, null, null, c.Status.ToString(), c.CreatedAt, c.UpdatedAt, null);
}
