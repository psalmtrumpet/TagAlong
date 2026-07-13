using TagAlong.Messaging.API.DTOs;
using TagAlong.Messaging.Domain.Entities;

namespace TagAlong.Messaging.API.Commands;

internal static class ConversationDtoMapper
{
    public static ConversationDto ToDto(Conversation c) => new(
        c.Id,
        c.PackageRequestId,
        c.SenderId,
        c.TravelerId,
        null,
        null,
        c.Status.ToString(),
        c.CreatedAt,
        c.UpdatedAt,
        null,
        c.RecipientUserId,
        c.RecipientName,
        c.AgreedPrice,
        c.LockInProposedBy,
        c.StartedAt,
        c.DeliveredAt,
        c.PassengerDestLat,
        c.PassengerDestLng,
        c.PassengerDestAddress);
}

internal static class MessageDtoMapper
{
    public static MessageDto ToDto(Message m) => new(
        m.Id,
        m.ConversationId,
        m.SenderId,
        m.Content,
        m.MessageType.ToString(),
        m.ProposedPrice,
        m.SentAt,
        m.ReadAt);
}
