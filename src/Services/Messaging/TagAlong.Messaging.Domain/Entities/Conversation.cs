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
    // Set when the ride is completed: the fare (AgreedPrice) splits into
    // TagAlong's fee and what the driver earns. Fixed at completion so a later
    // fee change doesn't rewrite past rides.
    public decimal? PlatformFee { get; private set; }
    public decimal? DriverEarning { get; private set; }
    /// <summary>TagAlong's fee was waived (the driver used a referral free ride).</summary>
    public bool FeeWaived { get; private set; }
    /// <summary>Booked on demand ("Ride now"): one passenger, no offered trip, private fee.</summary>
    public bool IsOnDemand { get; private set; }
    /// <summary>How many passengers this request is for (seats on a shared trip).</summary>
    public int Seats { get; private set; } = 1;
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
        string? dropName = null,
        bool isOnDemand = false,
        int seats = 1)
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
            IsOnDemand = isOnDemand,
            Seats = Math.Clamp(seats, 1, 10),
        };
    }

    /// <summary>An on-demand ride a driver just accepted: the price is agreed and they're on their way.</summary>
    /// <param name="driverLat">Where the driver was when they were matched (their starting point).</param>
    public void BookOnDemand(decimal price, double? driverLat = null, double? driverLng = null)
    {
        AgreedPrice = price;
        LockInProposedBy = null;
        Status = ConversationStatus.LockedIn;
        IssuePickupPin();
        MarkLockInPosition(driverLat, driverLng);
        SetUpdated();
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
        IssuePickupPin();
        // A recent live position is the driver's starting point; otherwise their next one
        if (HelperLastSeenAt > DateTime.UtcNow.AddMinutes(-5)) MarkLockInPosition(HelperLastLat, HelperLastLng);
        SetUpdated();
    }

    /// <summary>
    /// The passenger's 4-digit pickup PIN: only they see it, and the driver
    /// must enter it to start their ride. Null on deliveries and on rides
    /// booked before PINs existed (those start without one).
    /// </summary>
    public string? PickupPin { get; private set; }
    public int PinFailures { get; private set; }
    public DateTime? PinLockedUntil { get; private set; }

    public const int MaxPinFailures = 5;

    /// <summary>A new random PIN (also used to avoid two passengers on one trip sharing a PIN).</summary>
    public void IssuePickupPin()
    {
        if (IsDelivery) return;
        PickupPin = System.Security.Cryptography.RandomNumberGenerator.GetInt32(0, 10000).ToString("D4");
        PinFailures = 0;
        PinLockedUntil = null;
    }

    /// <summary>
    /// Checks the PIN the driver typed. After <see cref="MaxPinFailures"/> wrong
    /// tries entry locks for 5 minutes, so the PIN can't be guessed.
    /// </summary>
    public PinCheck CheckPickupPin(string? pin, DateTime now)
    {
        if (PickupPin is null) return PinCheck.Ok;
        if (PinLockedUntil > now) return PinCheck.Locked;
        if (pin?.Trim() == PickupPin)
        {
            PinFailures = 0;
            return PinCheck.Ok;
        }
        PinFailures++;
        if (PinFailures >= MaxPinFailures)
        {
            PinFailures = 0;
            PinLockedUntil = now.AddMinutes(5);
        }
        SetUpdated();
        return PinLockedUntil > now ? PinCheck.Locked : PinCheck.Wrong;
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
        // Distance counts from the pickup
        RideDistanceMetres = 0;
        DistanceMarkLat = HelperLastLat;
        DistanceMarkLng = HelperLastLng;
        SetUpdated();
    }

    /// <param name="platformFee">TagAlong's flat fee for this ride (never more than the fare).</param>
    /// <param name="feeWaived">The driver used a referral free ride: no fee this time.</param>
    public void MarkDelivered(decimal platformFee = 0, bool feeWaived = false)
    {
        if (Status != ConversationStatus.InProgress)
            throw new InvalidOperationException("Can only mark an in-progress trip as delivered");

        Status = ConversationStatus.Closed;
        DeliveredAt = DateTime.UtcNow;
        if (AgreedPrice is { } fare)
        {
            FeeWaived = feeWaived;
            PlatformFee = feeWaived ? 0 : Math.Clamp(platformFee, 0, fare);
            DriverEarning = fare - PlatformFee;
        }
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

    /// <summary>A new fare the driver asked after the destination changed, waiting for the passenger.</summary>
    public decimal? FareChangeAsked { get; private set; }

    public void AskFareChange(decimal price) { FareChangeAsked = price; SetUpdated(); }

    public void AcceptFareChange()
    {
        if (FareChangeAsked is null) return;
        AgreedPrice = FareChangeAsked;
        FareChangeAsked = null;
        SetUpdated();
    }

    public void DeclineFareChange() { FareChangeAsked = null; SetUpdated(); }

    /// <summary>The passenger on a private ride now wants to go somewhere else.</summary>
    public void ChangeDestination(double lat, double lng, string? name)
    {
        FareChangeAsked = null; // any ask was for the old destination
        DropLat = lat;
        DropLng = lng;
        DropName = name;
        PassengerDestLat = lat;
        PassengerDestLng = lng;
        PassengerDestAddress = name;
        SetUpdated();
    }

    /// <summary>Why the ride was cancelled, and by whom (null if it ended normally).</summary>
    public string? CancelReason { get; private set; }
    public Guid? CancelledBy { get; private set; }

    public void RecordCancellation(Guid by, string reason)
    {
        CancelledBy = by;
        CancelReason = reason.Length > 300 ? reason[..300] : reason;
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
        if (Status == ConversationStatus.InProgress) CountDistance(lat, lng);
        if (Status == ConversationStatus.LockedIn && LockInDriverLat == null) MarkLockInPosition(lat, lng);
        SetUpdated();
    }

    // ── Distance driven with the passenger on board ──

    /// <summary>How far the car went between pickup and drop-off, from the driver's live position.</summary>
    public double? RideDistanceMetres { get; private set; }
    /// <summary>The last point counted: moves on in steps, so GPS wobble while parked isn't counted as driving.</summary>
    public double? DistanceMarkLat { get; private set; }
    public double? DistanceMarkLng { get; private set; }

    private const double DistanceStepMetres = 25;
    /// <summary>A bigger jump is a GPS glitch or a long gap, not driving.</summary>
    private const double DistanceJumpMetres = 5000;

    private void CountDistance(double lat, double lng)
    {
        if (DistanceMarkLat is not { } markLat || DistanceMarkLng is not { } markLng)
        {
            DistanceMarkLat = lat;
            DistanceMarkLng = lng;
            RideDistanceMetres ??= 0;
            return;
        }
        var d = DistanceMetres(markLat, markLng, lat, lng);
        if (d < DistanceStepMetres) return;
        if (d <= DistanceJumpMetres) RideDistanceMetres = (RideDistanceMetres ?? 0) + d;
        DistanceMarkLat = lat;
        DistanceMarkLng = lng;
    }

    private static double DistanceMetres(double lat1, double lng1, double lat2, double lng2)
    {
        const double R = 6371000;
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLng = (lng2 - lng1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return 2 * R * Math.Asin(Math.Sqrt(a));
    }

    // ── Ride history ──

    /// <summary>Removed from the passenger's / driver's ride history (only their own list).</summary>
    public bool HiddenBySender { get; private set; }
    public bool HiddenByTraveler { get; private set; }

    public void HideFromHistory(Guid userId)
    {
        if (userId == SenderId) HiddenBySender = true;
        if (userId == TravelerId) HiddenByTraveler = true;
    }

    /// <summary>When the driver paid TagAlong's fee for this ride (null: still owed).</summary>
    public DateTime? FeeSettledAt { get; private set; }
    /// <summary>The payment that covered it (null for rides from before fees were collected).</summary>
    public Guid? FeePaymentId { get; private set; }

    // ── Driver cancelling before pickup ──

    /// <summary>Where the driver was when the price was agreed (or their first position after).</summary>
    public double? LockInDriverLat { get; private set; }
    public double? LockInDriverLng { get; private set; }

    private void MarkLockInPosition(double? lat, double? lng)
    {
        if (lat is null || lng is null) return;
        LockInDriverLat = lat;
        LockInDriverLng = lng;
    }

    /// <summary>Moved less than this since the price was agreed: still where they were.</summary>
    public const double NotMovedMetres = 200;
    /// <summary>This much closer to the pickup than at lock-in: on their way to the passenger.</summary>
    public const double HeadingThereMetres = 150;

    public enum DriverCancel { Allowed, OnTheirWay, LocationUnknown, PickedUp }

    /// <summary>
    /// Once the price is agreed a driver may only cancel while they haven't set
    /// off for the passenger: not moved, or not getting closer to the pickup.
    /// Never once the passenger is on board. Needs their live position.
    /// </summary>
    public DriverCancel CanDriverCancel(DateTime now)
    {
        if (Status == ConversationStatus.InProgress) return DriverCancel.PickedUp;
        if (Status != ConversationStatus.LockedIn) return DriverCancel.Allowed;
        if (HelperLastSeenAt is not { } seen || seen < now.AddMinutes(-3) ||
            HelperLastLat is not { } lat || HelperLastLng is not { } lng ||
            LockInDriverLat is not { } startLat || LockInDriverLng is not { } startLng)
            return DriverCancel.LocationUnknown;

        if (Metres(startLat, startLng, lat, lng) < NotMovedMetres) return DriverCancel.Allowed;
        var pLat = MeetLat ?? PickupLat;
        var pLng = MeetLng ?? PickupLng;
        if (pLat is null || pLng is null) return DriverCancel.OnTheirWay; // moved, and we can't tell where to
        var closer = Metres(startLat, startLng, pLat.Value, pLng.Value) - Metres(lat, lng, pLat.Value, pLng.Value);
        return closer > HeadingThereMetres ? DriverCancel.OnTheirWay : DriverCancel.Allowed;
    }

    private static double Metres(double lat1, double lng1, double lat2, double lng2)
    {
        const double r = 6371000;
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLng = (lng2 - lng1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return 2 * r * Math.Asin(Math.Sqrt(a));
    }

    public void SettleFee(Guid paymentId)
    {
        if (FeeSettledAt != null) return;
        FeeSettledAt = DateTime.UtcNow;
        FeePaymentId = paymentId;
    }
}

public enum PinCheck { Ok, Wrong, Locked }

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
