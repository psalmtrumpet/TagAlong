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

public record CreateConversationCommand(
    Guid SenderId,
    Guid TravelerId,
    Guid? PackageRequestId,
    string? InitialMessage,
    Guid? RecipientUserId = null,
    string? RecipientName = null,
    double? PassengerDestLat = null,
    double? PassengerDestLng = null,
    string? PassengerDestAddress = null,
    Guid? TripId = null,
    bool IsDelivery = false,
    double? PickupLat = null,
    double? PickupLng = null,
    string? PickupAddress = null,
    double? MeetLat = null,
    double? MeetLng = null,
    string? MeetName = null,
    double? DropLat = null,
    double? DropLng = null,
    string? DropName = null) : ICommand<ConversationDto>;

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
    private readonly IHubContext<MessagingHub, IMessagingClient> _hubContext;
    private readonly IEventBus _eventBus;
    private readonly ILogger<CreateConversationCommandHandler> _logger;

    public CreateConversationCommandHandler(
        IConversationRepository conversationRepository,
        IMessageRepository messageRepository,
        IHubContext<MessagingHub, IMessagingClient> hubContext,
        IEventBus eventBus,
        ILogger<CreateConversationCommandHandler> logger)
    {
        _conversationRepository = conversationRepository;
        _messageRepository = messageRepository;
        _hubContext = hubContext;
        _eventBus = eventBus;
        _logger = logger;
    }

    public async Task<Result<ConversationDto>> Handle(CreateConversationCommand request, CancellationToken cancellationToken)
    {
        // Reuse an open (not Declined/Closed) conversation for the same pair and trip.
        // A different trip, or a declined/closed conversation, starts a new one.
        var existingConversation = await _conversationRepository.GetOpenByParticipantsAsync(
            request.SenderId, request.TravelerId, request.TripId, cancellationToken);

        if (existingConversation != null)
        {
            _logger.LogInformation("Active conversation already exists between {SenderId} and {TravelerId}",
                request.SenderId, request.TravelerId);
            return Result.Success(MapToDto(existingConversation, null));
        }

        var conversation = Conversation.Create(
            request.SenderId, request.TravelerId, request.PackageRequestId,
            recipientUserId: request.RecipientUserId, recipientName: request.RecipientName,
            passengerDestLat: request.PassengerDestLat,
            passengerDestLng: request.PassengerDestLng,
            passengerDestAddress: request.PassengerDestAddress,
            tripId: request.TripId,
            isDelivery: request.IsDelivery,
            pickupLat: request.PickupLat,
            pickupLng: request.PickupLng,
            pickupAddress: request.PickupAddress,
            meetLat: request.MeetLat,
            meetLng: request.MeetLng,
            meetName: Trim(request.MeetName),
            dropLat: request.DropLat,
            dropLng: request.DropLng,
            dropName: Trim(request.DropName));
        await _conversationRepository.AddAsync(conversation, cancellationToken);
        await _conversationRepository.SaveChangesAsync(cancellationToken);

        Message? initialMessage = null;
        if (!string.IsNullOrWhiteSpace(request.InitialMessage))
        {
            initialMessage = Message.CreateTextMessage(conversation.Id, request.SenderId, request.InitialMessage);
            await _messageRepository.AddAsync(initialMessage, cancellationToken);
            await _messageRepository.SaveChangesAsync(cancellationToken);
        }

        // Push real-time notification to both traveler and sender via SignalR
        var dto = MapToDto(conversation, initialMessage);
        await _hubContext.Clients.Group($"user_{request.TravelerId}").ConversationUpdated(dto);
        await _hubContext.Clients.Group($"user_{request.SenderId}").ConversationUpdated(dto);

        // Also publish integration event (for notification API persistence)
        await _eventBus.PublishAsync(new ConversationRequestCreatedIntegrationEvent(
            conversation.Id,
            request.SenderId,
            request.TravelerId,
            request.InitialMessage ?? "Someone wants to use your trip.",
            DateTime.UtcNow), cancellationToken);

        _logger.LogInformation("Conversation {ConversationId} created between {SenderId} and {TravelerId}",
            conversation.Id, request.SenderId, request.TravelerId);

        return Result.Success(dto);
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
            lastMessageDto,
            conversation.RecipientUserId,
            conversation.RecipientName,
            conversation.AgreedPrice,
            conversation.LockInProposedBy,
            conversation.StartedAt,
            conversation.DeliveredAt,
            conversation.PassengerDestLat,
            conversation.PassengerDestLng,
            conversation.PassengerDestAddress,
            TripId: conversation.TripId,
            IsDelivery: conversation.IsDelivery,
            PickupLat: conversation.PickupLat,
            PickupLng: conversation.PickupLng,
            PickupAddress: conversation.PickupAddress,
            MeetLat: conversation.MeetLat,
            MeetLng: conversation.MeetLng,
            MeetName: conversation.MeetName,
            DropLat: conversation.DropLat,
            DropLng: conversation.DropLng,
            DropName: conversation.DropName);
    }

    private static string? Trim(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : (s.Trim().Length > 200 ? s.Trim()[..200] : s.Trim());
}
