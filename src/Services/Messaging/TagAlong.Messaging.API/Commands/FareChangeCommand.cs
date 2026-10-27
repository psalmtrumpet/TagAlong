using Microsoft.AspNetCore.SignalR;
using TagAlong.Common.CQRS;
using TagAlong.Common.Results;
using TagAlong.Messaging.API.DTOs;
using TagAlong.Messaging.API.Hubs;
using TagAlong.Messaging.Domain.Entities;
using TagAlong.Messaging.Domain.Repositories;

namespace TagAlong.Messaging.API.Commands;

/// <param name="Action">"ask" (driver, with Price), "accept" or "decline" (passenger).</param>
public record FareChangeCommand(Guid ConversationId, Guid UserId, string Action, decimal? Price) : ICommand<ConversationDto>;

/// <summary>
/// After a passenger changes the destination of a private ride, the driver can
/// ask a new fare; the passenger accepts it or keeps the agreed one.
/// </summary>
public class FareChangeCommandHandler : ICommandHandler<FareChangeCommand, ConversationDto>
{
    private readonly IConversationRepository _conversations;
    private readonly IMessageRepository _messages;
    private readonly IHubContext<MessagingHub, IMessagingClient> _hub;

    public FareChangeCommandHandler(IConversationRepository conversations, IMessageRepository messages,
        IHubContext<MessagingHub, IMessagingClient> hub)
    {
        _conversations = conversations;
        _messages = messages;
        _hub = hub;
    }

    public async Task<Result<ConversationDto>> Handle(FareChangeCommand request, CancellationToken ct)
    {
        var c = await _conversations.GetByIdAsync(request.ConversationId, ct);
        if (c == null) return Result.Failure<ConversationDto>(Error.NotFound("Ride not found"));
        if (c.IsDelivery || c.Status is not (ConversationStatus.LockedIn or ConversationStatus.InProgress))
            return Result.Failure<ConversationDto>(Error.Validation("The fare can only change on a ride that's booked or under way"));

        string text;
        switch (request.Action.ToLowerInvariant())
        {
            case "ask":
                if (c.TravelerId != request.UserId)
                    return Result.Failure<ConversationDto>(Error.Unauthorized("Only the driver can ask a new fare"));
                if (request.Price is not > 0 || request.Price == c.AgreedPrice)
                    return Result.Failure<ConversationDto>(Error.Validation("Enter a new fare"));
                c.AskFareChange(request.Price.Value);
                text = $"Driver asks {Naira(request.Price.Value)} for the new destination (was {Naira(c.AgreedPrice)})";
                break;
            case "accept":
            case "decline":
                if (c.SenderId != request.UserId)
                    return Result.Failure<ConversationDto>(Error.Unauthorized("Only the passenger can answer"));
                if (c.FareChangeAsked is null)
                    return Result.Failure<ConversationDto>(Error.Validation("There's no new fare to answer"));
                if (request.Action.Equals("accept", StringComparison.OrdinalIgnoreCase))
                {
                    c.AcceptFareChange();
                    text = $"Passenger accepted {Naira(c.AgreedPrice)} — that's the fare now";
                }
                else
                {
                    c.DeclineFareChange();
                    text = $"Passenger kept the fare at {Naira(c.AgreedPrice)}";
                }
                break;
            default:
                return Result.Failure<ConversationDto>(Error.Validation("Unknown action"));
        }

        _conversations.Update(c);
        var msg = Message.CreateSystemMessage(c.Id, text);
        await _messages.AddAsync(msg, ct);
        await _conversations.SaveChangesAsync(ct);

        var dto = ConversationDtoMapper.ToDto(c);
        var msgDto = MessageDtoMapper.ToDto(msg);
        foreach (var group in new[] { $"conversation_{c.Id}", $"user_{c.SenderId}", $"user_{c.TravelerId}" })
        {
            await _hub.Clients.Group(group).ReceiveMessage(msgDto);
            await _hub.Clients.Group(group).ConversationUpdated(dto);
        }
        return Result.Success(dto);
    }

    private static string Naira(decimal? amount) => amount is null ? "the agreed fare" : $"₦{amount:N0}";
}
