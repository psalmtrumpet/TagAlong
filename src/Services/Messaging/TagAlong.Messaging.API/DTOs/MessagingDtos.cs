namespace TagAlong.Messaging.API.DTOs;

public record ConversationDto(
    Guid Id,
    Guid? PackageRequestId,
    Guid SenderId,
    Guid TravelerId,
    string? SenderName,
    string? TravelerName,
    string Status,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    MessageDto? LastMessage);

public record MessageDto(
    Guid Id,
    Guid ConversationId,
    Guid SenderId,
    string Content,
    string MessageType,
    decimal? ProposedPrice,
    DateTime SentAt,
    DateTime? ReadAt);

public record CreateConversationRequest(
    Guid TravelerId,
    Guid? PackageRequestId,
    string? InitialMessage);

public record SendMessageRequest(
    string Content);

public record SendPriceProposalRequest(
    decimal ProposedPrice,
    string? Message);

public record AcceptPriceRequest(
    decimal AcceptedPrice,
    string? Message);

public record RejectPriceRequest(
    decimal? CounterPrice,
    string? Message);
