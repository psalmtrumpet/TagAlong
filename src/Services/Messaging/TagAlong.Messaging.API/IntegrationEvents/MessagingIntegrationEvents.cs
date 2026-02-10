using TagAlong.EventBus;

namespace TagAlong.Messaging.API.IntegrationEvents;

public record NegotiationMessageSentIntegrationEvent(
    Guid ConversationId,
    Guid? PackageRequestId,
    Guid SenderId,
    Guid RecipientId,
    string MessageType,
    decimal? ProposedPrice,
    DateTime SentAt) : IntegrationEvent;

public record PriceAcceptedIntegrationEvent(
    Guid ConversationId,
    Guid? PackageRequestId,
    Guid SenderId,
    Guid TravelerId,
    decimal AcceptedPrice,
    DateTime AcceptedAt) : IntegrationEvent;
