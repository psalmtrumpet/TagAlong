using TagAlong.Common.Domain;

namespace TagAlong.Messaging.Domain.Entities;

public class Conversation : AggregateRoot
{
    public Guid? PackageRequestId { get; private set; }
    public Guid? TripId { get; private set; }
    public bool IsDelivery { get; private set; }
    public double? PickupLat { get; private set; }
    public double? PickupLng { get; private set; }
    public string? PickupAddress { get; private set; }
    // Where the driver picks the passenger up and sets them down — always on
    // the driver's own route (bus stop / junction), so there's no detour
    public double? MeetLat { get; private set; }
    public double? MeetLng { get; private set; }
    public string? MeetName { get; private set; }
    public double? DropLat { get; private set; }
    public double? DropLng { get; private set; }
    public string? DropName { get; private set; }
    public Guid SenderId { get; private set; }
    public Guid TravelerId { get; private set; }
    public Guid? RecipientUserId { get; private set; }
    public string? RecipientName { get; private set; }
    public ConversationStatus Status { get; private set; } = ConversationStatus.Active;
    public decimal? AgreedPrice { get; private set; }
    public Guid? LockInProposedBy { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? DeliveredAt { get; private set; }
    public double? PassengerDestLat { get; private set; }
    public double? PassengerDestLng { get; private set; }
    public string? PassengerDestAddress { get; private set; }
    public double? HelperLastLat { get; private set; }
    public double? HelperLastLng { get; private set; }
    public DateTime? HelperLastSeenAt { get; private set; }

    private readonly List<Message> _messages = new();
    public IReadOnlyCollection<Message> Messages => _messages.AsReadOnly();

    private Conversation() { }

    public static Conversation Create(
        Guid senderId,
        Guid travelerId,
        Guid? packageRequestId = null,
        bool startAsPending = true,
        Guid? recipientUserId = null,
        string? recipientName = null,
        double? passengerDestLat = null,
        double? passengerDestLng = null,
        string? passengerDestAddress = null,
        Guid? tripId = null,
        bool isDelivery = false,
        double? pickupLat = null,
        double? pickupLng = null,
        string? pickupAddress = null,
        double? meetLat = null,
        double? meetLng = null,
        string? meetName = null,
        double? dropLat = null,
        double? dropLng = null,
        string? dropName = null)
    {
        return new Conversation
        {
            SenderId = senderId,
            TravelerId = travelerId,
            PackageRequestId = packageRequestId,
            Status = startAsPending ? ConversationStatus.Pending : ConversationStatus.Active,
            RecipientUserId = recipientUserId,
            RecipientName = recipientName,
            PassengerDestLat = passengerDestLat,
            PassengerDestLng = passengerDestLng,
            PassengerDestAddress = passengerDestAddress,
            TripId = tripId,
            IsDelivery = isDelivery || packageRequestId != null,
            PickupLat = pickupLat,
            PickupLng = pickupLng,
            PickupAddress = pickupAddress,
            MeetLat = meetLat,
            MeetLng = meetLng,
            MeetName = meetName,
            DropLat = dropLat,
            DropLng = dropLng,
            DropName = dropName,
        };
    }

    public void Accept()
    {
        if (Status != ConversationStatus.Pending)
            throw new InvalidOperationException("Can only accept a pending conversation");

        Status = ConversationStatus.Negotiating;
        SetUpdated();
    }

    public void ConfirmPriceAgreement(decimal agreedPrice)
    {
        if (Status != ConversationStatus.Negotiating && Status != ConversationStatus.Active)
            throw new InvalidOperationException("Conversation is not in a state where price can be confirmed");

        AgreedPrice = agreedPrice;
        Status = ConversationStatus.Active;
        SetUpdated();
    }

    public void ProposeLockIn(Guid proposedBy, decimal price)
    {
        if (Status != ConversationStatus.Active)
            throw new InvalidOperationException("Can only propose lock-in on an active conversation");

        AgreedPrice = price;
        LockInProposedBy = proposedBy;
        SetUpdated();
    }

    public void ConfirmLockIn()
    {
        if (Status != ConversationStatus.Active || LockInProposedBy == null)
            throw new InvalidOperationException("No pending lock-in to confirm");

        Status = ConversationStatus.LockedIn;
        LockInProposedBy = null;
        SetUpdated();
    }

    public void RejectLockIn()
    {
        if (LockInProposedBy == null)
            throw new InvalidOperationException("No pending lock-in to reject");

        LockInProposedBy = null;
        SetUpdated();
    }

    public void StartTrip()
    {
        if (Status != ConversationStatus.LockedIn)
            throw new InvalidOperationException("Can only start a locked-in trip");

        Status = ConversationStatus.InProgress;
        StartedAt = DateTime.UtcNow;
        SetUpdated();
    }

    public void MarkDelivered()
    {
        if (Status != ConversationStatus.InProgress)
            throw new InvalidOperationException("Can only mark an in-progress trip as delivered");

        Status = ConversationStatus.Closed;
        DeliveredAt = DateTime.UtcNow;
        SetUpdated();
    }

    public void Decline()
    {
        if (Status != ConversationStatus.Pending)
            throw new InvalidOperationException("Can only decline a pending conversation");

        Status = ConversationStatus.Declined;
        SetUpdated();
    }

    public void Close()
    {
        if (Status == ConversationStatus.Closed)
            throw new InvalidOperationException("Conversation is already closed");

        Status = ConversationStatus.Closed;
        SetUpdated();
    }

    public void Reopen()
    {
        if (Status != ConversationStatus.Closed)
            throw new InvalidOperationException("Only closed conversations can be reopened");

        Status = ConversationStatus.Active;
        SetUpdated();
    }

    /// <summary>The driver moves the pickup and/or drop-off point (before the ride starts for pickup).</summary>
    public void SetMeetPoints(double? meetLat, double? meetLng, string? meetName, double? dropLat, double? dropLng, string? dropName)
    {
        if (meetLat is not null && meetLng is not null)
        {
            MeetLat = meetLat;
            MeetLng = meetLng;
            MeetName = meetName;
        }
        if (dropLat is not null && dropLng is not null)
        {
            DropLat = dropLat;
            DropLng = dropLng;
            DropName = dropName;
        }
        SetUpdated();
    }

    public bool IsParticipant(Guid userId)
    {
        return SenderId == userId || TravelerId == userId;
    }

    public Guid GetOtherParticipant(Guid userId)
    {
        return SenderId == userId ? TravelerId : SenderId;
    }

    public void AddMessage(Message message)
    {
        _messages.Add(message);
        SetUpdated();
    }

    public void UpdateHelperLocation(double lat, double lng)
    {
        HelperLastLat = lat;
        HelperLastLng = lng;
        HelperLastSeenAt = DateTime.UtcNow;
        SetUpdated();
    }
}

public enum ConversationStatus
{
    Pending,
    Negotiating,
    Active,
    LockedIn,
    InProgress,
    Declined,
    Closed
}
