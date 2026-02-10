using TagAlong.EventBus;

namespace TagAlong.Payment.API.IntegrationEvents;

public record PaymentInitiatedIntegrationEvent(
    Guid PaymentId,
    Guid DeliveryId,
    Guid SenderId,
    Guid TravelerId,
    decimal Amount,
    decimal PlatformFee,
    decimal TravelerPayout) : IntegrationEvent;

public record PaymentCompletedIntegrationEvent(
    Guid PaymentId,
    Guid DeliveryId,
    Guid SenderId,
    Guid TravelerId,
    decimal Amount,
    decimal PlatformFee,
    decimal TravelerPayout,
    string TransactionReference,
    DateTime PaidAt) : IntegrationEvent;

public record PaymentFailedIntegrationEvent(
    Guid PaymentId,
    Guid DeliveryId,
    Guid SenderId,
    Guid TravelerId,
    decimal Amount,
    string? Reason) : IntegrationEvent;

public record PaymentRefundedIntegrationEvent(
    Guid PaymentId,
    Guid DeliveryId,
    Guid SenderId,
    Guid TravelerId,
    decimal Amount,
    string? Reason) : IntegrationEvent;
