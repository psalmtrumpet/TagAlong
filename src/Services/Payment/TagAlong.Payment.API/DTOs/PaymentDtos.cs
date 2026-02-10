namespace TagAlong.Payment.API.DTOs;

public record PaymentDto(
    Guid Id,
    Guid DeliveryId,
    Guid SenderId,
    Guid TravelerId,
    decimal Amount,
    decimal PlatformFee,
    decimal TravelerPayout,
    string Status,
    string? PaymentMethod,
    string? TransactionReference,
    string? PaymentProvider,
    DateTime? PaidAt,
    DateTime CreatedAt);

public record PaymentSummaryDto(
    decimal TotalEarnings,
    decimal TotalSpent,
    int CompletedPayments,
    int PendingPayments);

public record InitiatePaymentRequest(
    Guid DeliveryId,
    Guid SenderId,
    Guid TravelerId,
    decimal Amount,
    string? PaymentMethod,
    string? PaymentProvider);

public record ConfirmPaymentRequest(
    string TransactionReference);

public record RefundPaymentRequest(
    string? Reason);
