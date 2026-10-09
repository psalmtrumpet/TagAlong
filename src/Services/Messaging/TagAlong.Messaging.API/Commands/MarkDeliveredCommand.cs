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

public record MarkDeliveredCommand(Guid ConversationId, Guid UserId) : ICommand<ConversationDto>;

public class MarkDeliveredCommandHandler : ICommandHandler<MarkDeliveredCommand, ConversationDto>
{
    private readonly IConversationRepository _conversationRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly IHubContext<MessagingHub, IMessagingClient> _hubContext;
    private readonly IEventBus _eventBus;

    public MarkDeliveredCommandHandler(
        IConversationRepository conversationRepository,
        IMessageRepository messageRepository,
        IHubContext<MessagingHub, IMessagingClient> hubContext,
        IEventBus eventBus,
        IConfiguration config)
    {
        _conversationRepository = conversationRepository;
        _messageRepository = messageRepository;
        _hubContext = hubContext;
        _eventBus = eventBus;
        _config = config;
    }

    private readonly IConfiguration _config;

    /// <summary>TagAlong's flat fee per ride (Earnings:RideFlatFee, default ₦200).</summary>
    public static decimal RideFlatFee(IConfiguration config) =>
        decimal.TryParse(config["Earnings:RideFlatFee"], System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture, out var fee) && fee >= 0 ? fee : 200m;

    public async Task<Result<ConversationDto>> Handle(MarkDeliveredCommand request, CancellationToken cancellationToken)
    {
        var conversation = await _conversationRepository.GetByIdAsync(request.ConversationId, cancellationToken);
        if (conversation == null)
            return Result.Failure<ConversationDto>(Error.NotFound("Conversation not found"));

        if (conversation.TravelerId != request.UserId)
            return Result.Failure<ConversationDto>(Error.Unauthorized("Only the traveler can mark as delivered"));

        conversation.MarkDelivered(RideFlatFee(_config));
        _conversationRepository.Update(conversation);

        var msg = Message.CreateDelivered(request.ConversationId, request.UserId);
        await _messageRepository.AddAsync(msg, cancellationToken);
        await _conversationRepository.SaveChangesAsync(cancellationToken);

        await ConversationLifecyclePublisher.TravelerTripStateAsync(_conversationRepository, _eventBus, conversation.TravelerId, cancellationToken);
        await ConversationLifecyclePublisher.TripBookingsAsync(_conversationRepository, _eventBus, conversation, cancellationToken);
        await ConversationLifecyclePublisher.CompletedAsync(_eventBus, conversation, cancellationToken);

        var dto = ConversationDtoMapper.ToDto(conversation);
        var msgDto = MessageDtoMapper.ToDto(msg);

        await _hubContext.Clients.Group($"conversation_{request.ConversationId}").ReceiveMessage(msgDto);
        await _hubContext.Clients.Group($"conversation_{request.ConversationId}").DeliveryCompleted(dto);

        var otherUserId = conversation.GetOtherParticipant(request.UserId);
        await _hubContext.Clients.Group($"user_{otherUserId}").ReceiveMessage(msgDto);
        await _hubContext.Clients.Group($"user_{otherUserId}").DeliveryCompleted(dto);

        return Result.Success(dto);
    }
}
