using TagAlong.Messaging.API.DTOs;

namespace TagAlong.Messaging.API.Hubs;

public interface IMessagingClient
{
    Task ReceiveMessage(MessageDto message);
    Task MessageRead(Guid messageId, DateTime readAt);
    Task ConversationUpdated(ConversationDto conversation);
    Task PriceAccepted(Guid conversationId, decimal acceptedPrice);
    Task ReceiveHelperLocation(string conversationId, double latitude, double longitude);
    Task LockInProposed(ConversationDto conversation);
    Task LockInConfirmed(ConversationDto conversation);
    Task LockInRejected(ConversationDto conversation);
    Task DeliveryStarted(ConversationDto conversation);
    Task DeliveryCompleted(ConversationDto conversation);
}
