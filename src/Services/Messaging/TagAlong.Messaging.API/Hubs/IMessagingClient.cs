using TagAlong.Messaging.API.DTOs;

namespace TagAlong.Messaging.API.Hubs;

public interface IMessagingClient
{
    Task ReceiveMessage(MessageDto message);
    Task MessageRead(Guid messageId, DateTime readAt);
    Task ConversationUpdated(ConversationDto conversation);
    Task PriceAccepted(Guid conversationId, decimal acceptedPrice);
}
