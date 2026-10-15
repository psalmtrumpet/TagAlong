using FluentValidation;
using Microsoft.AspNetCore.SignalR;
using TagAlong.Common.CQRS;
using TagAlong.Common.Results;
using TagAlong.EventBus;
using TagAlong.Messaging.API.DTOs;
using TagAlong.Messaging.API.Hubs;
using TagAlong.Messaging.API.IntegrationEvents;
using TagAlong.Messaging.Domain.Entities;
using TagAlong.Messaging.Domain.Repositories;

namespace TagAlong.Messaging.API.Commands;

public record AcceptPriceCommand(
    Guid ConversationId,
    Guid UserId,
    decimal AcceptedPrice,
    string? Message) : ICommand<MessageDto>;

public class AcceptPriceCommandValidator : AbstractValidator<AcceptPriceCommand>
{
    public AcceptPriceCommandValidator()
    {
        RuleFor(x => x.ConversationId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.AcceptedPrice).GreaterThan(0);
        RuleFor(x => x.Message).MaximumLength(2000).When(x => x.Message != null);
    }
}

public class AcceptPriceCommandHandler : ICommandHandler<AcceptPriceCommand, MessageDto>
{
    private readonly IConversationRepository _conversationRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly IHubContext<MessagingHub, IMessagingClient> _hubContext;
    private readonly IEventBus _eventBus;
    private readonly ILogger<AcceptPriceCommandHandler> _logger;

    public AcceptPriceCommandHandler(
        IConversationRepository conversationRepository,
        IMessageRepository messageRepository,
        IHubContext<MessagingHub, IMessagingClient> hubContext,
        IEventBus eventBus,
        ILogger<AcceptPriceCommandHandler> logger)
    {
        _conversationRepository = conversationRepository;
        _messageRepository = messageRepository;
        _hubContext = hubContext;
        _eventBus = eventBus;
        _logger = logger;
    }

    public async Task<Result<MessageDto>> Handle(AcceptPriceCommand request, CancellationToken cancellationToken)
    {
        var conversation = await _conversationRepository.GetByIdAsync(request.ConversationId, cancellationToken);
        if (conversation == null)
        {
            return Result.Failure<MessageDto>(Error.NotFound("Conversation not found"));
        }

        if (!conversation.IsParticipant(request.UserId))
        {
            return Result.Failure<MessageDto>(Error.Unauthorized("Not authorized to accept price in this conversation"));
        }

        // Only the latest offer can be accepted, and only by the person it was made to —
        // a countered (older) price or your own offer can't be accepted
        var latest = await _messageRepository.GetLatestPriceProposalAsync(request.ConversationId, cancellationToken);
        if (latest == null || latest.MessageType == MessageType.PriceAccepted)
            return Result.Failure<MessageDto>(Error.Validation(latest == null
                ? "There's no price offer to accept."
                : "A price has already been agreed."));
        if (latest.SenderId == request.UserId)
            return Result.Failure<MessageDto>(Error.Validation("You can't accept your own offer — wait for their reply."));
        if (latest.ProposedPrice != request.AcceptedPrice)
            return Result.Failure<MessageDto>(Error.Validation(
                $"That offer was replaced by a newer one (₦{latest.ProposedPrice:N0}). Accept or counter the latest price."));

        // Transition conversation to Active — chat is now unlocked
        conversation.ConfirmPriceAgreement(request.AcceptedPrice);
        _conversationRepository.Update(conversation);

        var message = Message.CreatePriceAccepted(request.ConversationId, request.UserId, request.AcceptedPrice, request.Message);
        await _messageRepository.AddAsync(message, cancellationToken);

        // System message confirming chat is unlocked
        var systemMsg = Message.CreateSystemMessage(request.ConversationId,
            $"Price agreed at ₦{request.AcceptedPrice:N0}. You can now chat freely and track each other.");
        await _messageRepository.AddAsync(systemMsg, cancellationToken);

        await _conversationRepository.SaveChangesAsync(cancellationToken);

        var dto = MapToDto(message);

        await _hubContext.Clients.Group($"conversation_{request.ConversationId}").ReceiveMessage(dto);
        await _hubContext.Clients.Group($"conversation_{request.ConversationId}").PriceAccepted(request.ConversationId, request.AcceptedPrice);

        var otherUserId = conversation.GetOtherParticipant(request.UserId);
        await _hubContext.Clients.Group($"user_{otherUserId}").ReceiveMessage(dto);

        // Publish price accepted event
        await _eventBus.PublishAsync(new PriceAcceptedIntegrationEvent(
            request.ConversationId,
            conversation.PackageRequestId,
            conversation.SenderId,
            conversation.TravelerId,
            request.AcceptedPrice,
            DateTime.UtcNow));

        _logger.LogInformation("Price {Price} accepted in conversation {ConversationId}",
            request.AcceptedPrice, request.ConversationId);

        return Result.Success(dto);
    }

    private static MessageDto MapToDto(Message message) => new(
        message.Id,
        message.ConversationId,
        message.SenderId,
        message.Content,
        message.MessageType.ToString(),
        message.ProposedPrice,
        message.SentAt,
        message.ReadAt);
}
