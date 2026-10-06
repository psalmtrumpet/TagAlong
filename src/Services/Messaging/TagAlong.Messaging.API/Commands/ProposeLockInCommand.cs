using FluentValidation;
using Microsoft.AspNetCore.SignalR;
using TagAlong.Common.CQRS;
using TagAlong.Common.Results;
using TagAlong.Messaging.API.DTOs;
using TagAlong.Messaging.API.Hubs;
using TagAlong.Messaging.Domain.Entities;
using TagAlong.Messaging.Domain.Repositories;

namespace TagAlong.Messaging.API.Commands;

public record ProposeLockInCommand(Guid ConversationId, Guid UserId, decimal AgreedPrice) : ICommand<ConversationDto>;

public class ProposeLockInCommandValidator : AbstractValidator<ProposeLockInCommand>
{
    public ProposeLockInCommandValidator()
    {
        RuleFor(x => x.ConversationId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.AgreedPrice).GreaterThan(0);
    }
}

public class ProposeLockInCommandHandler : ICommandHandler<ProposeLockInCommand, ConversationDto>
{
    private readonly IConversationRepository _conversationRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly IHubContext<MessagingHub, IMessagingClient> _hubContext;

    public ProposeLockInCommandHandler(
        IConversationRepository conversationRepository,
        IMessageRepository messageRepository,
        IHubContext<MessagingHub, IMessagingClient> hubContext)
    {
        _conversationRepository = conversationRepository;
        _messageRepository = messageRepository;
        _hubContext = hubContext;
    }

    public async Task<Result<ConversationDto>> Handle(ProposeLockInCommand request, CancellationToken cancellationToken)
    {
        var conversation = await _conversationRepository.GetByIdAsync(request.ConversationId, cancellationToken);
        if (conversation == null)
            return Result.Failure<ConversationDto>(Error.NotFound("Conversation not found"));

        if (!conversation.IsParticipant(request.UserId))
            return Result.Failure<ConversationDto>(Error.Unauthorized("Not a participant in this conversation"));

        // Lock-in confirms the price already agreed; changing it needs a new
        // price proposal (otherwise this silently overwrote the agreement).
        if (conversation.AgreedPrice is { } agreed && agreed != request.AgreedPrice)
            return Result.Failure<ConversationDto>(Error.Validation(
                $"Lock in the agreed price of ₦{agreed:N0}, or agree a new price in the chat first."));

        conversation.ProposeLockIn(request.UserId, request.AgreedPrice);
        _conversationRepository.Update(conversation);

        var msg = Message.CreateLockIn(request.ConversationId, request.UserId, request.AgreedPrice);
        await _messageRepository.AddAsync(msg, cancellationToken);
        await _conversationRepository.SaveChangesAsync(cancellationToken);

        var dto = ConversationDtoMapper.ToDto(conversation);
        var msgDto = MessageDtoMapper.ToDto(msg);

        await _hubContext.Clients.Group($"conversation_{request.ConversationId}").ReceiveMessage(msgDto);
        await _hubContext.Clients.Group($"conversation_{request.ConversationId}").LockInProposed(dto);

        var otherUserId = conversation.GetOtherParticipant(request.UserId);
        await _hubContext.Clients.Group($"user_{otherUserId}").ReceiveMessage(msgDto);
        await _hubContext.Clients.Group($"user_{otherUserId}").LockInProposed(dto);

        return Result.Success(dto);
    }
}
