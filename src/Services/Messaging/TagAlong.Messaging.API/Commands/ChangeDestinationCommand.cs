using Microsoft.AspNetCore.SignalR;
using TagAlong.Common.CQRS;
using TagAlong.Common.Results;
using TagAlong.Messaging.API.DTOs;
using TagAlong.Messaging.API.Hubs;
using TagAlong.Messaging.Domain.Entities;
using TagAlong.Messaging.Domain.Repositories;

namespace TagAlong.Messaging.API.Commands;

public record ChangeDestinationCommand(Guid ConversationId, Guid UserId, double Latitude, double Longitude, string? Name) : ICommand<ConversationDto>;

/// <summary>
/// On a private ride the passenger can change where they're going — before or
/// during the journey. The driver's map re-routes at once. (Shared rides drop at
/// a stop on the driver's route, so there it stays the driver's call.)
/// </summary>
public class ChangeDestinationCommandHandler : ICommandHandler<ChangeDestinationCommand, ConversationDto>
{
    private readonly IConversationRepository _conversations;
    private readonly IMessageRepository _messages;
    private readonly IHubContext<MessagingHub, IMessagingClient> _hub;
    private readonly IHttpClientFactory _http;

    public ChangeDestinationCommandHandler(
        IConversationRepository conversations,
        IMessageRepository messages,
        IHubContext<MessagingHub, IMessagingClient> hub,
        IHttpClientFactory http)
    {
        _conversations = conversations;
        _messages = messages;
        _hub = hub;
        _http = http;
    }

    public async Task<Result<ConversationDto>> Handle(ChangeDestinationCommand request, CancellationToken ct)
    {
        var conversation = await _conversations.GetByIdAsync(request.ConversationId, ct);
        if (conversation == null)
            return Result.Failure<ConversationDto>(Error.NotFound("Ride not found"));
        if (conversation.SenderId != request.UserId)
            return Result.Failure<ConversationDto>(Error.Unauthorized("Only the passenger can change where they're going"));
        if (conversation.IsDelivery)
            return Result.Failure<ConversationDto>(Error.Validation("A delivery's destination can't be changed"));
        if (conversation.Status is not (ConversationStatus.LockedIn or ConversationStatus.InProgress))
            return Result.Failure<ConversationDto>(Error.Validation("You can change the destination once the ride is booked"));
        if (!conversation.IsOnDemand && !await IsPrivateTripAsync(conversation.TripId, ct))
            return Result.Failure<ConversationDto>(Error.Validation("On a shared ride you're dropped at a stop on the driver's route — ask the driver to move it"));

        var name = string.IsNullOrWhiteSpace(request.Name) ? null : request.Name.Trim()[..Math.Min(request.Name.Trim().Length, 200)];
        conversation.ChangeDestination(request.Latitude, request.Longitude, name);
        _conversations.Update(conversation);

        var msg = Message.CreateSystemMessage(conversation.Id,
            $"Passenger changed the destination to {name?.Split(',')[0].Trim() ?? "a new place"}");
        await _messages.AddAsync(msg, ct);
        await _conversations.SaveChangesAsync(ct);

        // Both sides at once — the driver's map re-routes on this
        var dto = ConversationDtoMapper.ToDto(conversation);
        var msgDto = MessageDtoMapper.ToDto(msg);
        foreach (var group in new[] { $"conversation_{conversation.Id}", $"user_{conversation.SenderId}", $"user_{conversation.TravelerId}" })
        {
            await _hub.Clients.Group(group).ReceiveMessage(msgDto);
            await _hub.Clients.Group(group).ConversationUpdated(dto);
        }
        return Result.Success(dto);
    }

    private async Task<bool> IsPrivateTripAsync(Guid? tripId, CancellationToken ct)
    {
        if (tripId == null) return false;
        try
        {
            using var res = await _http.CreateClient("trip-api").GetAsync($"api/trips/{tripId}", ct);
            if (!res.IsSuccessStatusCode) return false;
            using var doc = System.Text.Json.JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
            return doc.RootElement.TryGetProperty("isPrivate", out var p) && p.ValueKind == System.Text.Json.JsonValueKind.True;
        }
        catch
        {
            return false;
        }
    }
}
