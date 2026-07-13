using Microsoft.AspNetCore.SignalR;
using TagAlong.Common.CQRS;
using TagAlong.Common.Results;
using TagAlong.Messaging.API.DTOs;
using TagAlong.Messaging.API.Hubs;
using TagAlong.Messaging.Domain.Entities;
using TagAlong.Messaging.Domain.Repositories;

namespace TagAlong.Messaging.API.Commands;

public record ConfirmLockInCommand(Guid ConversationId, Guid UserId) : ICommand<ConversationDto>;

public class ConfirmLockInCommandHandler : ICommandHandler<ConfirmLockInCommand, ConversationDto>
{
    private readonly IConversationRepository _conversationRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly IHubContext<MessagingHub, IMessagingClient> _hubContext;

    public ConfirmLockInCommandHandler(
        IConversationRepository conversationRepository,
        IMessageRepository messageRepository,
        IHubContext<MessagingHub, IMessagingClient> hubContext)
    {
        _conversationRepository = conversationRepository;
        _messageRepository = messageRepository;
        _hubContext = hubContext;
    }

    public async Task<Result<ConversationDto>> Handle(ConfirmLockInCommand request, CancellationToken cancellationToken)
    {
        var conversation = await _conversationRepository.GetByIdAsync(request.ConversationId, cancellationToken);
        if (conversation == null)
            return Result.Failure<ConversationDto>(Error.NotFound("Conversation not found"));

        if (!conversation.IsParticipant(request.UserId))
            return Result.Failure<ConversationDto>(Error.Unauthorized("Not a participant in this conversation"));

        var price = conversation.AgreedPrice ?? 0;
        conversation.ConfirmLockIn();
        _conversationRepository.Update(conversation);

        var msg = Message.CreateLockInConfirmed(request.ConversationId, request.UserId, price);
        await _messageRepository.AddAsync(msg, cancellationToken);
        await _conversationRepository.SaveChangesAsync(cancellationToken);

        var dto = ConversationDtoMapper.ToDto(conversation);
        var msgDto = MessageDtoMapper.ToDto(msg);

        await _hubContext.Clients.Group($"conversation_{request.ConversationId}").ReceiveMessage(msgDto);
        await _hubContext.Clients.Group($"conversation_{request.ConversationId}").LockInConfirmed(dto);

        var otherUserId = conversation.GetOtherParticipant(request.UserId);
        await _hubContext.Clients.Group($"user_{otherUserId}").ReceiveMessage(msgDto);
        await _hubContext.Clients.Group($"user_{otherUserId}").LockInConfirmed(dto);

        return Result.Success(dto);
    }
}
