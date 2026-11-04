using TagAlong.Common.Domain;

namespace TagAlong.Messaging.Domain.Entities;

/// <summary>
/// A driver paying TagAlong's fees for a set of completed rides (through
/// Paystack). The rides it covers are fixed when the payment starts; they're
/// marked settled once Paystack confirms the money arrived.
/// </summary>
public class DriverFeePayment : Entity
{
    public Guid DriverId { get; private set; }
    /// <summary>Our reference, sent to Paystack and echoed back on confirmation.</summary>
    public string Reference { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public int Rides { get; private set; }
    /// <summary>The rides this payment covers (conversation ids, comma separated).</summary>
    public string RideIds { get; private set; } = string.Empty;
    public string? AuthorizationUrl { get; private set; }
    public DriverFeePaymentStatus Status { get; private set; } = DriverFeePaymentStatus.Pending;
    public DateTime? PaidAt { get; private set; }

    private DriverFeePayment() { }

    public static DriverFeePayment Start(Guid driverId, decimal amount, IReadOnlyCollection<Guid> rideIds) => new()
    {
        DriverId = driverId,
        Reference = $"TAF-{Guid.NewGuid():N}"[..24].ToUpperInvariant(),
        Amount = amount,
        Rides = rideIds.Count,
        RideIds = string.Join(',', rideIds),
    };

    public IEnumerable<Guid> RideIdList =>
        RideIds.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(Guid.Parse);

    public void SetAuthorizationUrl(string url)
    {
        AuthorizationUrl = url;
        SetUpdated();
    }

    public void MarkPaid()
    {
        Status = DriverFeePaymentStatus.Paid;
        PaidAt = DateTime.UtcNow;
        SetUpdated();
    }

    public void MarkFailed()
    {
        if (Status != DriverFeePaymentStatus.Pending) return;
        Status = DriverFeePaymentStatus.Failed;
        SetUpdated();
    }
}

public enum DriverFeePaymentStatus
{
    Pending,
    Paid,
    Failed,
}
