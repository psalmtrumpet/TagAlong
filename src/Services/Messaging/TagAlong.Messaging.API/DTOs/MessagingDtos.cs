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
    MessageDto? LastMessage,
    Guid? RecipientUserId = null,
    string? RecipientName = null,
    decimal? AgreedPrice = null,
    Guid? LockInProposedBy = null,
    DateTime? StartedAt = null,
    DateTime? DeliveredAt = null,
    double? PassengerDestLat = null,
    double? PassengerDestLng = null,
    string? PassengerDestAddress = null,
    double? HelperLastLat = null,
    double? HelperLastLng = null);

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
    string? InitialMessage,
    Guid? RecipientUserId = null,
    string? RecipientName = null,
    double? PassengerDestLat = null,
    double? PassengerDestLng = null,
    string? PassengerDestAddress = null);

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

public record ProposeLockInRequest(
    decimal AgreedPrice);
