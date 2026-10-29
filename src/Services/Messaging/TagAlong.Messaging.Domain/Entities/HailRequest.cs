using TagAlong.Common.Domain;

namespace TagAlong.Messaging.Domain.Entities;

/// <summary>
/// An on-demand ride request: a passenger wants a private ride now, from
/// pickup to destination, at an offered price. It goes to every available
/// driver within a short drive; the first to accept (or the driver whose
/// counter-offer the passenger accepts) gets the ride.
/// </summary>
public class HailRequest : Entity
{
    public Guid PassengerId { get; private set; }
    public double PickupLat { get; private set; }
    public double PickupLng { get; private set; }
    public string PickupName { get; private set; } = string.Empty;
    public double DestLat { get; private set; }
    public double DestLng { get; private set; }
    public string DestName { get; private set; } = string.Empty;
    public decimal OfferedPrice { get; private set; }
    public HailStatus Status { get; private set; } = HailStatus.Open;
    public Guid? MatchedDriverId { get; private set; }
    public Guid? ConversationId { get; private set; }
    public DateTime ExpiresAt { get; private set; }

    private HailRequest() { }

    public static HailRequest Create(
        Guid passengerId,
        double pickupLat, double pickupLng, string pickupName,
        double destLat, double destLng, string destName,
        decimal offeredPrice, TimeSpan lifetime) => new()
    {
        PassengerId = passengerId,
        PickupLat = pickupLat,
        PickupLng = pickupLng,
        PickupName = pickupName,
        DestLat = destLat,
        DestLng = destLng,
        DestName = destName,
        OfferedPrice = offeredPrice,
        ExpiresAt = DateTime.UtcNow + lifetime,
    };

    public bool IsOpen => Status == HailStatus.Open && ExpiresAt > DateTime.UtcNow;

    /// <summary>The passenger raised their offer and is asking again.</summary>
    public void Raise(decimal offeredPrice, TimeSpan lifetime)
    {
        OfferedPrice = offeredPrice;
        Status = HailStatus.Open;
        ExpiresAt = DateTime.UtcNow + lifetime;
        SetUpdated();
    }

    public void LinkConversation(Guid conversationId)
    {
        ConversationId = conversationId;
        SetUpdated();
    }

    /// <summary>Why the passenger cancelled (null if they didn't say).</summary>
    public string? CancelReason { get; private set; }

    public void Cancel(string? reason = null)
    {
        Status = HailStatus.Cancelled;
        if (!string.IsNullOrWhiteSpace(reason)) CancelReason = reason.Trim().Length > 300 ? reason.Trim()[..300] : reason.Trim();
        SetUpdated();
    }

    public void Expire()
    {
        Status = HailStatus.Expired;
        SetUpdated();
    }
}

public enum HailStatus
{
    Open,
    Matched,
    Cancelled,
    Expired
}

/// <summary>A driver an on-demand request was sent to, and what they did with it.</summary>
public class HailCandidate : Entity
{
    public Guid HailId { get; private set; }
    public Guid DriverId { get; private set; }
    public string DriverName { get; private set; } = string.Empty;
    public decimal DriverRating { get; private set; }
    public int DriverTotalRatings { get; private set; }
    public string Vehicle { get; private set; } = string.Empty;
    public string VehiclePlate { get; private set; } = string.Empty;
    /// <summary>Drive time to the pickup (including finishing the current ride).</summary>
    public int EtaSeconds { get; private set; }
    /// <summary>The driver is dropping someone off first.</summary>
    public bool FinishingCurrentRide { get; private set; }
    /// <summary>Where the driver was when asked (their live ride position if finishing one).</summary>
    public double DriverLat { get; private set; }
    public double DriverLng { get; private set; }
    /// <summary>Rough road distance to the pickup, for display.</summary>
    public int DistanceMeters { get; private set; }
    public decimal? CounterPrice { get; private set; }
    public HailCandidateStatus Status { get; private set; } = HailCandidateStatus.Notified;

    private HailCandidate() { }

    public static HailCandidate Create(
        Guid hailId, Guid driverId, string driverName, decimal rating, int totalRatings,
        string vehicle, string plate, int etaSeconds, bool finishingCurrentRide,
        double driverLat, double driverLng, int distanceMeters) => new()
    {
        DriverLat = driverLat,
        DriverLng = driverLng,
        DistanceMeters = distanceMeters,
        HailId = hailId,
        DriverId = driverId,
        DriverName = driverName,
        DriverRating = rating,
        DriverTotalRatings = totalRatings,
        Vehicle = vehicle,
        VehiclePlate = plate,
        EtaSeconds = etaSeconds,
        FinishingCurrentRide = finishingCurrentRide,
    };

    public void Counter(decimal price)
    {
        CounterPrice = price;
        Status = HailCandidateStatus.Countered;
        SetUpdated();
    }

    public void Decline()
    {
        Status = HailCandidateStatus.Declined;
        SetUpdated();
    }

    /// <summary>Sent again after the passenger raised their offer.</summary>
    /// <summary>The passenger chose not to wait for this driver (too far).</summary>
    public void Skip()
    {
        Status = HailCandidateStatus.Skipped;
        SetUpdated();
    }

    public void Renotify(int etaSeconds, bool finishingCurrentRide, double driverLat, double driverLng, int distanceMeters)
    {
        DriverLat = driverLat;
        DriverLng = driverLng;
        DistanceMeters = distanceMeters;
        EtaSeconds = etaSeconds;
        FinishingCurrentRide = finishingCurrentRide;
        CounterPrice = null;
        Status = HailCandidateStatus.Notified;
        SetUpdated();
    }
}

public enum HailCandidateStatus
{
    Notified,
    Countered,
    Declined,
    Skipped
}
