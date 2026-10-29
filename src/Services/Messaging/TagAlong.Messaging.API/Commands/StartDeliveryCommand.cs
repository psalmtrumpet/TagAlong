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

/// <param name="Pin">The passenger's pickup PIN, typed by the driver (rides only).</param>
public record StartDeliveryCommand(Guid ConversationId, Guid UserId, string? Pin = null) : ICommand<ConversationDto>;

public class StartDeliveryCommandHandler : ICommandHandler<StartDeliveryCommand, ConversationDto>
{
    private readonly IConversationRepository _conversationRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly IHubContext<MessagingHub, IMessagingClient> _hubContext;
    private readonly IEventBus _eventBus;

    public StartDeliveryCommandHandler(
        IConversationRepository conversationRepository,
        IMessageRepository messageRepository,
        IHubContext<MessagingHub, IMessagingClient> hubContext,
        IEventBus eventBus)
    {
        _conversationRepository = conversationRepository;
        _messageRepository = messageRepository;
        _hubContext = hubContext;
        _eventBus = eventBus;
    }

    public async Task<Result<ConversationDto>> Handle(StartDeliveryCommand request, CancellationToken cancellationToken)
    {
        var conversation = await _conversationRepository.GetByIdAsync(request.ConversationId, cancellationToken);
        if (conversation == null)
            return Result.Failure<ConversationDto>(Error.NotFound("Conversation not found"));

        if (conversation.TravelerId != request.UserId)
            return Result.Failure<ConversationDto>(Error.Unauthorized("Only the traveler can start the trip"));

        if (conversation.IsOnDemand && conversation.Status == ConversationStatus.LockedIn &&
            await _conversationRepository.CountInProgressByTravelerIdAsync(conversation.TravelerId, cancellationToken) > 0)
            return Result.Failure<ConversationDto>(Error.Validation("Drop off your current passenger before starting this ride."));

        if (conversation.Status != ConversationStatus.LockedIn)
            return Result.Failure<ConversationDto>(Error.Validation("This ride can't be started right now."));

        // The passenger's PIN proves the right person is getting in
        if (conversation.PickupPin != null)
        {
            var now = DateTime.UtcNow;
            if (string.IsNullOrWhiteSpace(request.Pin) && !(conversation.PinLockedUntil > now))
                return Result.Failure<ConversationDto>(Error.Validation(
                    "Enter your passenger's 4-digit PIN to start the ride. Update TagAlong if you don't see where to type it."));
            var check = conversation.CheckPickupPin(request.Pin, now);
            if (check != PinCheck.Ok)
            {
                _conversationRepository.Update(conversation);
                await _conversationRepository.SaveChangesAsync(cancellationToken); // count the wrong try
                return Result.Failure<ConversationDto>(Error.Validation(
                    TagAlong.Messaging.API.Services.PickupPins.Problem(check, conversation, now)));
            }
        }

        conversation.StartTrip();
        _conversationRepository.Update(conversation);

        var msg = Message.CreateTripStarted(request.ConversationId, request.UserId);
        await _messageRepository.AddAsync(msg, cancellationToken);
        await _conversationRepository.SaveChangesAsync(cancellationToken);

        await ConversationLifecyclePublisher.TravelerTripStateAsync(_conversationRepository, _eventBus, conversation.TravelerId, cancellationToken);

        var dto = ConversationDtoMapper.ToDto(conversation);
        var msgDto = MessageDtoMapper.ToDto(msg);

        await _hubContext.Clients.Group($"conversation_{request.ConversationId}").ReceiveMessage(msgDto);
        await _hubContext.Clients.Group($"conversation_{request.ConversationId}").DeliveryStarted(dto);

        var otherUserId = conversation.GetOtherParticipant(request.UserId);
        await _hubContext.Clients.Group($"user_{otherUserId}").ReceiveMessage(msgDto);
        await _hubContext.Clients.Group($"user_{otherUserId}").DeliveryStarted(dto);

        return Result.Success(dto);
    }
}
