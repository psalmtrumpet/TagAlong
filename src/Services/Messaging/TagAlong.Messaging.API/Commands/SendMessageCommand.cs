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

public record SendMessageCommand(
    Guid ConversationId,
    Guid SenderId,
    string Content) : ICommand<MessageDto>;

public class SendMessageCommandValidator : AbstractValidator<SendMessageCommand>
{
    public SendMessageCommandValidator()
    {
        RuleFor(x => x.ConversationId).NotEmpty();
        RuleFor(x => x.SenderId).NotEmpty();
        RuleFor(x => x.Content).NotEmpty().MaximumLength(2000);
    }
}

public class SendMessageCommandHandler : ICommandHandler<SendMessageCommand, MessageDto>
{
    private readonly IConversationRepository _conversationRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly IHubContext<MessagingHub, IMessagingClient> _hubContext;
    private readonly IEventBus _eventBus;
    private readonly ILogger<SendMessageCommandHandler> _logger;

    public SendMessageCommandHandler(
        IConversationRepository conversationRepository,
        IMessageRepository messageRepository,
        IHubContext<MessagingHub, IMessagingClient> hubContext,
        IEventBus eventBus,
        ILogger<SendMessageCommandHandler> logger)
    {
        _conversationRepository = conversationRepository;
        _messageRepository = messageRepository;
        _hubContext = hubContext;
        _eventBus = eventBus;
        _logger = logger;
    }

    public async Task<Result<MessageDto>> Handle(SendMessageCommand request, CancellationToken cancellationToken)
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

        var message = Message.CreateTextMessage(request.ConversationId, request.SenderId, request.Content);
        await _messageRepository.AddAsync(message, cancellationToken);
        await _messageRepository.SaveChangesAsync(cancellationToken);

        var dto = MapToDto(message);

        // Notify via SignalR
        await _hubContext.Clients.Group($"conversation_{request.ConversationId}").ReceiveMessage(dto);
        var otherUserId = conversation.GetOtherParticipant(request.SenderId);
        await _hubContext.Clients.Group($"user_{otherUserId}").ReceiveMessage(dto);

        // Publish integration event so the notification service can push to the receiver's device
        await _eventBus.PublishAsync(new NegotiationMessageSentIntegrationEvent(
            request.ConversationId,
            conversation.PackageRequestId,
            request.SenderId,
            otherUserId,
            message.MessageType.ToString(),
            null,
            message.SentAt), cancellationToken);

        _logger.LogInformation("Message {MessageId} sent in conversation {ConversationId}", message.Id, request.ConversationId);

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
