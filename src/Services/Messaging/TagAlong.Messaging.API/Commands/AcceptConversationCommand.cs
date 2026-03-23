using TagAlong.Common.CQRS;
using TagAlong.Common.Results;
using TagAlong.Messaging.API.DTOs;
using TagAlong.Messaging.Domain.Entities;
using TagAlong.Messaging.Domain.Repositories;

namespace TagAlong.Messaging.API.Commands;

public record AcceptConversationCommand(Guid ConversationId, Guid TravelerId) : ICommand<ConversationDto>;

public class AcceptConversationCommandHandler : ICommandHandler<AcceptConversationCommand, ConversationDto>
{
    private readonly IConversationRepository _conversationRepository;
    private readonly IMessageRepository _messageRepository;

    public AcceptConversationCommandHandler(
        IConversationRepository conversationRepository,
        IMessageRepository messageRepository)
    {
        _conversationRepository = conversationRepository;
        _messageRepository = messageRepository;
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

        return Result.Success(MapToDto(conversation));
    }

    private static ConversationDto MapToDto(Conversation c) =>
        new(c.Id, c.PackageRequestId, c.SenderId, c.TravelerId, c.Status.ToString(), c.CreatedAt, c.UpdatedAt, null);
}
