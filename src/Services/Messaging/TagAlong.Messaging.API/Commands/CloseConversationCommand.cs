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

/// <param name="Reason">Why they cancelled (picked from a list, or typed). Older apps send none.</param>
public record CloseConversationCommand(Guid ConversationId, Guid UserId, string? Reason = null) : ICommand<ConversationDto>;

public class CloseConversationCommandHandler : ICommandHandler<CloseConversationCommand, ConversationDto>
{
    private readonly IConversationRepository _conversationRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly IHubContext<MessagingHub, IMessagingClient> _hubContext;
    private readonly IEventBus _eventBus;

    public CloseConversationCommandHandler(
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

    public async Task<Result<ConversationDto>> Handle(CloseConversationCommand request, CancellationToken cancellationToken)
    {
        var conversation = await _conversationRepository.GetByIdAsync(request.ConversationId, cancellationToken);
        if (conversation == null)
            return Result.Failure<ConversationDto>(new Error("Conversation.NotFound", "Conversation not found"));

        if (!conversation.IsParticipant(request.UserId))
            return Result.Failure<ConversationDto>(new Error("Conversation.Forbidden", "Not a participant in this conversation"));

        var wasInProgress = conversation.Status == ConversationStatus.InProgress;
        var heldBooking = wasInProgress || conversation.Status == ConversationStatus.LockedIn;
        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        if (reason != null) conversation.RecordCancellation(request.UserId, reason);
        conversation.Close();
        _conversationRepository.Update(conversation);

        // With a reason, say who cancelled and why (both sides and the admin portal see it)
        var who = conversation.TravelerId == request.UserId
            ? (conversation.IsDelivery ? "The carrier" : "The driver")
            : (conversation.IsDelivery ? "The sender" : "The passenger");
        var systemMsg = Message.CreateSystemMessage(conversation.Id, reason == null
            ? "The chat has been ended."
            : $"{who} cancelled the ride: {reason}");
        await _messageRepository.AddAsync(systemMsg, cancellationToken);

        await _conversationRepository.SaveChangesAsync(cancellationToken);

        if (wasInProgress)
            await ConversationLifecyclePublisher.TravelerTripStateAsync(_conversationRepository, _eventBus, conversation.TravelerId, cancellationToken);
        if (heldBooking)
            await ConversationLifecyclePublisher.TripBookingsAsync(_conversationRepository, _eventBus, conversation, cancellationToken);

        var dto = MapToDto(conversation);

        // Notify the other party
        var otherUserId = conversation.GetOtherParticipant(request.UserId);
        await _hubContext.Clients.Group($"user_{otherUserId}").ConversationUpdated(dto);
        await _hubContext.Clients.Group($"conversation_{request.ConversationId}").ConversationUpdated(dto);

        return Result.Success(dto);
    }

    private static ConversationDto MapToDto(Conversation c) => ConversationDtoMapper.ToDto(c);
}
