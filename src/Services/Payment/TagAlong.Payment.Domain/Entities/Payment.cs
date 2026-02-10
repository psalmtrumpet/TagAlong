using TagAlong.Common.Domain;

namespace TagAlong.Payment.Domain.Entities;

public class Payment : AggregateRoot
{
    public Guid DeliveryId { get; private set; }
    public Guid SenderId { get; private set; }
    public Guid TravelerId { get; private set; }
    public decimal Amount { get; private set; }
    public decimal PlatformFee { get; private set; }
    public decimal TravelerPayout { get; private set; }
    public PaymentStatus Status { get; private set; } = PaymentStatus.Pending;
    public string? PaymentMethod { get; private set; }
    public string? TransactionReference { get; private set; }
    public string? PaymentProvider { get; private set; }
    public DateTime? PaidAt { get; private set; }

    private Payment() { }

    public static Payment Create(
        Guid deliveryId,
        Guid senderId,
        Guid travelerId,
        decimal amount,
        decimal platformFeePercentage,
        string? paymentMethod = null,
        string? paymentProvider = null)
    {
        var platformFee = amount * (platformFeePercentage / 100);
        var travelerPayout = amount - platformFee;

        return new Payment
        {
            DeliveryId = deliveryId,
            SenderId = senderId,
            TravelerId = travelerId,
            Amount = amount,
            PlatformFee = platformFee,
            TravelerPayout = travelerPayout,
            PaymentMethod = paymentMethod,
            PaymentProvider = paymentProvider
        };
    }

    public void SetPaymentMethod(string method)
    {
        PaymentMethod = method;
        SetUpdated();
    }

    public void SetPaymentProvider(string provider)
    {
        PaymentProvider = provider;
        SetUpdated();
    }

    public void MarkAsProcessing(string transactionReference)
    {
        if (Status != PaymentStatus.Pending)
            throw new InvalidOperationException("Only pending payments can be marked as processing");

        Status = PaymentStatus.Processing;
        TransactionReference = transactionReference;
        SetUpdated();
    }

    public void MarkAsCompleted(string? transactionReference = null)
    {
        if (Status != PaymentStatus.Pending && Status != PaymentStatus.Processing)
            throw new InvalidOperationException("Only pending or processing payments can be completed");

        Status = PaymentStatus.Completed;
        PaidAt = DateTime.UtcNow;
        if (!string.IsNullOrEmpty(transactionReference))
        {
            TransactionReference = transactionReference;
        }
        SetUpdated();
    }

    public void MarkAsFailed(string? reason = null)
    {
        if (Status == PaymentStatus.Completed || Status == PaymentStatus.Refunded)
            throw new InvalidOperationException("Cannot mark a completed or refunded payment as failed");

        Status = PaymentStatus.Failed;
        SetUpdated();
    }

    public void MarkAsRefunded()
    {
        if (Status != PaymentStatus.Completed)
            throw new InvalidOperationException("Only completed payments can be refunded");

        Status = PaymentStatus.Refunded;
        SetUpdated();
    }

    public void UpdateTransactionReference(string reference)
    {
        TransactionReference = reference;
        SetUpdated();
    }
}

public enum PaymentStatus
{
    Pending,
    Processing,
    Completed,
    Failed,
    Refunded
}
