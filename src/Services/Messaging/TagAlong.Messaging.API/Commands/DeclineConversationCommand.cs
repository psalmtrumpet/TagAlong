using TagAlong.Common.CQRS;
using TagAlong.Common.Results;
using TagAlong.Messaging.API.DTOs;
using TagAlong.Messaging.Domain.Entities;
using TagAlong.Messaging.Domain.Repositories;

namespace TagAlong.Messaging.API.Commands;

public record DeclineConversationCommand(Guid ConversationId, Guid TravelerId) : ICommand<ConversationDto>;

public class DeclineConversationCommandHandler : ICommandHandler<DeclineConversationCommand, ConversationDto>
{
    private readonly IConversationRepository _conversationRepository;
    private readonly IMessageRepository _messageRepository;

    public DeclineConversationCommandHandler(
        IConversationRepository conversationRepository,
        IMessageRepository messageRepository)
    {
        _conversationRepository = conversationRepository;
        _messageRepository = messageRepository;
    }

    public async Task<Result<ConversationDto>> Handle(DeclineConversationCommand request, CancellationToken cancellationToken)
    {
        var conversation = await _conversationRepository.GetByIdAsync(request.ConversationId, cancellationToken);
        if (conversation == null)
            return Result.Failure<ConversationDto>(new Error("Conversation.NotFound", "Conversation not found"));

        if (conversation.TravelerId != request.TravelerId)
            return Result.Failure<ConversationDto>(new Error("Conversation.Forbidden", "Only the traveler can decline this request"));

        conversation.Decline();
        _conversationRepository.Update(conversation);

        var systemMsg = Message.CreateSystemMessage(conversation.Id, "Request declined.");
        await _messageRepository.AddAsync(systemMsg, cancellationToken);

        await _conversationRepository.SaveChangesAsync(cancellationToken);

        return Result.Success(MapToDto(conversation));
    }

    private static ConversationDto MapToDto(Conversation c) =>
        new(c.Id, c.PackageRequestId, c.SenderId, c.TravelerId, null, null, c.Status.ToString(), c.CreatedAt, c.UpdatedAt, null);
}
