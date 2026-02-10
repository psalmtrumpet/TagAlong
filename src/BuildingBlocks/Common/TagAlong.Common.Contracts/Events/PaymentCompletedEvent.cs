namespace TagAlong.Common.Contracts.Events;

public record PaymentCompletedEvent(
    Guid PaymentId,
    Guid DeliveryId,
    Guid SenderId,
    Guid TravelerId,
    decimal Amount,
    decimal PlatformFee,
    decimal TravelerPayout,
    DateTime CompletedAt);
