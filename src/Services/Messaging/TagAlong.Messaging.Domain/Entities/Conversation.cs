using TagAlong.Common.Domain;

namespace TagAlong.Messaging.Domain.Entities;

public class Conversation : AggregateRoot
{
    public Guid? PackageRequestId { get; private set; }
    public Guid SenderId { get; private set; }
    public Guid TravelerId { get; private set; }
    public ConversationStatus Status { get; private set; } = ConversationStatus.Active;

    private readonly List<Message> _messages = new();
    public IReadOnlyCollection<Message> Messages => _messages.AsReadOnly();

    private Conversation() { }

    public static Conversation Create(
        Guid senderId,
        Guid travelerId,
        Guid? packageRequestId = null,
        bool startAsPending = true)
    {
        return new Conversation
        {
            SenderId = senderId,
            TravelerId = travelerId,
            PackageRequestId = packageRequestId,
            Status = startAsPending ? ConversationStatus.Pending : ConversationStatus.Active
        };
    }

    public void Accept()
    {
        if (Status != ConversationStatus.Pending)
            throw new InvalidOperationException("Can only accept a pending conversation");

        Status = ConversationStatus.Active;
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
    Active,
    Declined,
    Closed
}
