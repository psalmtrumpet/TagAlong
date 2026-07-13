using TagAlong.Common.Domain;

namespace TagAlong.Messaging.Domain.Entities;

public class Conversation : AggregateRoot
{
    public Guid? PackageRequestId { get; private set; }
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
        string? passengerDestAddress = null)
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
