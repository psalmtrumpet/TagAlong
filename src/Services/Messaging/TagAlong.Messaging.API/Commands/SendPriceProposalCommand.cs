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

public record SendPriceProposalCommand(
    Guid ConversationId,
    Guid SenderId,
    decimal ProposedPrice,
    string? Message) : ICommand<MessageDto>;

public class SendPriceProposalCommandValidator : AbstractValidator<SendPriceProposalCommand>
{
    public SendPriceProposalCommandValidator()
    {
        RuleFor(x => x.ConversationId).NotEmpty();
        RuleFor(x => x.SenderId).NotEmpty();
        RuleFor(x => x.ProposedPrice).GreaterThan(0);
        RuleFor(x => x.Message).MaximumLength(2000).When(x => x.Message != null);
    }
}

public class SendPriceProposalCommandHandler : ICommandHandler<SendPriceProposalCommand, MessageDto>
{
    private readonly IConversationRepository _conversationRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly IHubContext<MessagingHub, IMessagingClient> _hubContext;
    private readonly IEventBus _eventBus;
    private readonly ILogger<SendPriceProposalCommandHandler> _logger;

    public SendPriceProposalCommandHandler(
        IConversationRepository conversationRepository,
        IMessageRepository messageRepository,
        IHubContext<MessagingHub, IMessagingClient> hubContext,
        IEventBus eventBus,
        ILogger<SendPriceProposalCommandHandler> logger)
    {
        _conversationRepository = conversationRepository;
        _messageRepository = messageRepository;
        _hubContext = hubContext;
        _eventBus = eventBus;
        _logger = logger;
    }

    public async Task<Result<MessageDto>> Handle(SendPriceProposalCommand request, CancellationToken cancellationToken)
    {
        var conversation = await _conversationRepository.GetByIdAsync(request.ConversationId, cancellationToken);
        if (conversation == null)
        {
            return Result.Failure<MessageDto>(Error.NotFound("Conversation not found"));
        }

        if (!conversation.IsParticipant(request.SenderId))
        {
            return Result.Failure<MessageDto>(Error.Unauthorized("Not authorized to send messages in this conversation"));
        }

        var message = Message.CreatePriceProposal(request.ConversationId, request.SenderId, request.ProposedPrice, request.Message);
        await _messageRepository.AddAsync(message, cancellationToken);
        await _messageRepository.SaveChangesAsync(cancellationToken);

        var dto = MapToDto(message);

        await _hubContext.Clients.Group($"conversation_{request.ConversationId}").ReceiveMessage(dto);
        var otherUserId = conversation.GetOtherParticipant(request.SenderId);
        await _hubContext.Clients.Group($"user_{otherUserId}").ReceiveMessage(dto);

        // Publish event for notification
        await _eventBus.PublishAsync(new NegotiationMessageSentIntegrationEvent(
            request.ConversationId,
            conversation.PackageRequestId,
            request.SenderId,
            otherUserId,
            "PriceProposal",
            request.ProposedPrice,
            DateTime.UtcNow));

        _logger.LogInformation("Price proposal {Price} sent in conversation {ConversationId}",
            request.ProposedPrice, request.ConversationId);

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
