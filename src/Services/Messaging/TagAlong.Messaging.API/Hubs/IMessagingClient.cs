using TagAlong.Messaging.API.DTOs;

namespace TagAlong.Messaging.API.Hubs;

public interface IMessagingClient
{
    Task ReceiveMessage(MessageDto message);
    Task MessageRead(Guid messageId, DateTime readAt);
    Task ConversationUpdated(ConversationDto conversation);
    Task PriceAccepted(Guid conversationId, decimal acceptedPrice);
    /// <summary>The other person's live position. The reading details come last so older apps
    /// (which read only the first three) still work.</summary>
    Task ReceiveHelperLocation(string conversationId, double latitude, double longitude,
        double? accuracy, double? speed, double? heading, long? timestampMs);
    Task LockInProposed(ConversationDto conversation);
    Task LockInConfirmed(ConversationDto conversation);
    Task LockInRejected(ConversationDto conversation);
    Task DeliveryStarted(ConversationDto conversation);
    Task DeliveryCompleted(ConversationDto conversation);

    // On-demand rides
    Task HailRequested(TagAlong.Messaging.API.Hails.HailOfferDto offer);
    Task HailClosed(Guid hailId, string reason);
    Task HailCounterOffer(TagAlong.Messaging.API.Hails.HailCounterDto counter);
    Task HailMatched(Guid hailId, ConversationDto conversation);
    Task HailExpired(Guid hailId);

    // In-app calls between a driver and passenger
    Task IncomingCall(TagAlong.Messaging.API.Calls.IncomingCallDto call);
    Task CallAnswered(Guid callId);
    Task CallEnded(Guid callId, string reason);
    /// <summary>Call setup from the other phone: "offer", "answer" or "candidate" (JSON).</summary>
    Task CallSignal(Guid callId, string type, string payload);
}
