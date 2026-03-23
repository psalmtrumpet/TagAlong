using FluentValidation;
using TagAlong.Common.CQRS;
using TagAlong.Common.Results;
using TagAlong.EventBus;
using TagAlong.Messaging.API.DTOs;
using TagAlong.Messaging.API.IntegrationEvents;
using TagAlong.Messaging.Domain.Entities;
using TagAlong.Messaging.Domain.Repositories;

namespace TagAlong.Messaging.API.Commands;

public record CreateConversationCommand(
    Guid SenderId,
    Guid TravelerId,
    Guid? PackageRequestId,
    string? InitialMessage) : ICommand<ConversationDto>;

public class CreateConversationCommandValidator : AbstractValidator<CreateConversationCommand>
{
    public CreateConversationCommandValidator()
    {
        RuleFor(x => x.SenderId).NotEmpty();
        RuleFor(x => x.TravelerId).NotEmpty();
        RuleFor(x => x.SenderId).NotEqual(x => x.TravelerId).WithMessage("Cannot create conversation with yourself");
        RuleFor(x => x.InitialMessage).MaximumLength(2000).When(x => x.InitialMessage != null);
    }
}

public class CreateConversationCommandHandler : ICommandHandler<CreateConversationCommand, ConversationDto>
{
    private readonly IConversationRepository _conversationRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly IEventBus _eventBus;
    private readonly ILogger<CreateConversationCommandHandler> _logger;

    public CreateConversationCommandHandler(
        IConversationRepository conversationRepository,
        IMessageRepository messageRepository,
        IEventBus eventBus,
        ILogger<CreateConversationCommandHandler> logger)
    {
        _conversationRepository = conversationRepository;
        _messageRepository = messageRepository;
        _eventBus = eventBus;
        _logger = logger;
    }

    public async Task<Result<ConversationDto>> Handle(CreateConversationCommand request, CancellationToken cancellationToken)
    {
        // Check if conversation already exists
        var existingConversation = await _conversationRepository.GetByParticipantsAsync(
            request.SenderId, request.TravelerId, cancellationToken);

        if (existingConversation != null)
        {
            _logger.LogInformation("Conversation already exists between {SenderId} and {TravelerId}",
                request.SenderId, request.TravelerId);
            return Result.Success(MapToDto(existingConversation, null));
        }

        var conversation = Conversation.Create(request.SenderId, request.TravelerId, request.PackageRequestId);
        await _conversationRepository.AddAsync(conversation, cancellationToken);
        await _conversationRepository.SaveChangesAsync(cancellationToken);

        Message? initialMessage = null;
        if (!string.IsNullOrWhiteSpace(request.InitialMessage))
        {
            initialMessage = Message.CreateTextMessage(conversation.Id, request.SenderId, request.InitialMessage);
            await _messageRepository.AddAsync(initialMessage, cancellationToken);
            await _messageRepository.SaveChangesAsync(cancellationToken);
        }

        // Notify the traveler they have a new request
        await _eventBus.PublishAsync(new ConversationRequestCreatedIntegrationEvent(
            conversation.Id,
            request.SenderId,
            request.TravelerId,
            request.InitialMessage ?? "Someone wants to use your trip.",
            DateTime.UtcNow), cancellationToken);

        _logger.LogInformation("Conversation {ConversationId} created between {SenderId} and {TravelerId}",
            conversation.Id, request.SenderId, request.TravelerId);

        return Result.Success(MapToDto(conversation, initialMessage));
    }

    private static ConversationDto MapToDto(Conversation conversation, Message? lastMessage)
    {
        MessageDto? lastMessageDto = lastMessage != null
            ? new MessageDto(
                lastMessage.Id,
                lastMessage.ConversationId,
                lastMessage.SenderId,
                lastMessage.Content,
                lastMessage.MessageType.ToString(),
                lastMessage.ProposedPrice,
                lastMessage.SentAt,
                lastMessage.ReadAt)
            : null;

        return new ConversationDto(
            conversation.Id,
            conversation.PackageRequestId,
            conversation.SenderId,
            conversation.TravelerId,
            null,
            null,
            conversation.Status.ToString(),
            conversation.CreatedAt,
            conversation.UpdatedAt,
            lastMessageDto);
    }
}
