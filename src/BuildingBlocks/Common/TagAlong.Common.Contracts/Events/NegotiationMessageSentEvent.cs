namespace TagAlong.Common.Contracts.Events;

public record NegotiationMessageSentEvent(
    Guid ConversationId,
    Guid SenderId,
    Guid ReceiverId,
    Guid? PackageRequestId,
    string MessageType,
    string Content,
    decimal? ProposedPrice,
    DateTime SentAt);
