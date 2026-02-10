using TagAlong.Common.Domain;

namespace TagAlong.Package.Domain.Entities;

public class Delivery : AggregateRoot
{
    public Guid PackageRequestId { get; private set; }
    public Guid TripId { get; private set; }
    public Guid SenderId { get; private set; }
    public Guid TravelerId { get; private set; }
    public decimal AgreedPrice { get; private set; }
    public decimal PlatformFee { get; private set; }
    public decimal TravelerPayout { get; private set; }
    public DeliveryStatus Status { get; private set; } = DeliveryStatus.Pending;
    public string? MeetupLocation { get; private set; }
    public double? MeetupLatitude { get; private set; }
    public double? MeetupLongitude { get; private set; }
    public DateTime? MeetupTime { get; private set; }
    public DateTime? PickedUpAt { get; private set; }
    public DateTime? DeliveredAt { get; private set; }
    public string? DeliveryProofImageUrl { get; private set; }
    public string? ReceiverName { get; private set; }
    public string? ReceiverPhone { get; private set; }
    public string? Notes { get; private set; }

    private Delivery() { }

    public static Delivery Create(
        Guid packageRequestId,
        Guid tripId,
        Guid senderId,
        Guid travelerId,
        decimal agreedPrice,
        decimal platformFeePercentage,
        string? meetupLocation = null,
        double? meetupLatitude = null,
        double? meetupLongitude = null,
        DateTime? meetupTime = null,
        string? receiverName = null,
        string? receiverPhone = null)
    {
        var platformFee = agreedPrice * (platformFeePercentage / 100);
        var travelerPayout = agreedPrice - platformFee;

        return new Delivery
        {
            PackageRequestId = packageRequestId,
            TripId = tripId,
            SenderId = senderId,
            TravelerId = travelerId,
            AgreedPrice = agreedPrice,
            PlatformFee = platformFee,
            TravelerPayout = travelerPayout,
            MeetupLocation = meetupLocation,
            MeetupLatitude = meetupLatitude,
            MeetupLongitude = meetupLongitude,
            MeetupTime = meetupTime,
            ReceiverName = receiverName,
            ReceiverPhone = receiverPhone
        };
    }

    public void Accept()
    {
        if (Status != DeliveryStatus.Pending)
            throw new InvalidOperationException("Only pending deliveries can be accepted");

        Status = DeliveryStatus.Accepted;
        SetUpdated();
    }

    public void Reject()
    {
        if (Status != DeliveryStatus.Pending)
            throw new InvalidOperationException("Only pending deliveries can be rejected");

        Status = DeliveryStatus.Rejected;
        SetUpdated();
    }

    public void MarkAsPickedUp()
    {
        if (Status != DeliveryStatus.Accepted)
            throw new InvalidOperationException("Only accepted deliveries can be picked up");

        Status = DeliveryStatus.PickedUp;
        PickedUpAt = DateTime.UtcNow;
        SetUpdated();
    }

    public void MarkAsInTransit()
    {
        if (Status != DeliveryStatus.PickedUp)
            throw new InvalidOperationException("Only picked up deliveries can be in transit");

        Status = DeliveryStatus.InTransit;
        SetUpdated();
    }

    public void MarkAsDelivered(string? proofImageUrl = null)
    {
        if (Status != DeliveryStatus.InTransit && Status != DeliveryStatus.PickedUp)
            throw new InvalidOperationException("Only in transit or picked up deliveries can be delivered");

        Status = DeliveryStatus.Delivered;
        DeliveredAt = DateTime.UtcNow;
        DeliveryProofImageUrl = proofImageUrl;
        SetUpdated();
    }

    public void Cancel()
    {
        if (Status == DeliveryStatus.Delivered)
            throw new InvalidOperationException("Cannot cancel a delivered package");

        Status = DeliveryStatus.Cancelled;
        SetUpdated();
    }

    public void UpdateMeetup(string location, double latitude, double longitude, DateTime time)
    {
        MeetupLocation = location;
        MeetupLatitude = latitude;
        MeetupLongitude = longitude;
        MeetupTime = time;
        SetUpdated();
    }

    public void UpdateNotes(string notes)
    {
        Notes = notes;
        SetUpdated();
    }
}

public enum DeliveryStatus
{
    Pending,
    Accepted,
    Rejected,
    PickedUp,
    InTransit,
    Delivered,
    Cancelled
}
