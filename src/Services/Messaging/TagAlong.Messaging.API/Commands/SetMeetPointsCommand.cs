using Microsoft.AspNetCore.SignalR;
using TagAlong.Common.CQRS;
using TagAlong.Common.Results;
using TagAlong.Messaging.API.DTOs;
using TagAlong.Messaging.API.Hubs;
using TagAlong.Messaging.Domain.Entities;
using TagAlong.Messaging.Domain.Repositories;

namespace TagAlong.Messaging.API.Commands;

public record SetMeetPointsCommand(Guid ConversationId, Guid UserId, SetMeetPointsRequest Points) : ICommand<ConversationDto>;

/// <summary>
/// Only the driver chooses where to pick up and set down — they're on a
/// journey with other people and must not detour. Both sides see the change.
/// </summary>
public class SetMeetPointsCommandHandler : ICommandHandler<SetMeetPointsCommand, ConversationDto>
{
    private readonly IConversationRepository _conversationRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly IHubContext<MessagingHub, IMessagingClient> _hubContext;

    public SetMeetPointsCommandHandler(
        IConversationRepository conversationRepository,
        IMessageRepository messageRepository,
        IHubContext<MessagingHub, IMessagingClient> hubContext)
    {
        _conversationRepository = conversationRepository;
        _messageRepository = messageRepository;
        _hubContext = hubContext;
    }

    public async Task<Result<ConversationDto>> Handle(SetMeetPointsCommand request, CancellationToken cancellationToken)
    {
        var conversation = await _conversationRepository.GetByIdAsync(request.ConversationId, cancellationToken);
        if (conversation == null)
            return Result.Failure<ConversationDto>(Error.NotFound("Conversation not found"));
        if (conversation.TravelerId != request.UserId)
            return Result.Failure<ConversationDto>(Error.Unauthorized("Only the driver can change the meet point"));
        if (conversation.Status is ConversationStatus.Closed or ConversationStatus.Declined)
            return Result.Failure<ConversationDto>(Error.Validation("This ride has ended"));

        var p = request.Points;
        var movingPickup = p.MeetLat is not null && p.MeetLng is not null;
        if (movingPickup && conversation.Status == ConversationStatus.InProgress)
            return Result.Failure<ConversationDto>(Error.Validation("The passenger has already been picked up"));

        conversation.SetMeetPoints(p.MeetLat, p.MeetLng, Clip(p.MeetName), p.DropLat, p.DropLng, Clip(p.DropName));
        _conversationRepository.Update(conversation);

        var parts = new List<string>();
        if (movingPickup) parts.Add($"pickup: {conversation.MeetName ?? "a new spot on the route"}");
        if (p.DropLat is not null && p.DropLng is not null) parts.Add($"drop-off: {conversation.DropName ?? "a new spot on the route"}");
        var msg = Message.CreateSystemMessage(request.ConversationId, $"Driver changed the {string.Join(" and ", parts)}");
        await _messageRepository.AddAsync(msg, cancellationToken);
        await _conversationRepository.SaveChangesAsync(cancellationToken);

        var dto = ConversationDtoMapper.ToDto(conversation);
        var msgDto = MessageDtoMapper.ToDto(msg);
        foreach (var group in new[] { $"conversation_{request.ConversationId}", $"user_{conversation.SenderId}", $"user_{conversation.TravelerId}" })
        {
            await _hubContext.Clients.Group(group).ReceiveMessage(msgDto);
            await _hubContext.Clients.Group(group).ConversationUpdated(dto);
        }
        return Result.Success(dto);
    }

    private static string? Clip(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : (s.Trim().Length > 200 ? s.Trim()[..200] : s.Trim());
}
