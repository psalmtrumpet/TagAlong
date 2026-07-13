using TagAlong.Common.Domain;

namespace TagAlong.Messaging.Domain.Entities;

public class Message : Entity
{
    public Guid ConversationId { get; private set; }
    public Guid SenderId { get; private set; }
    public string Content { get; private set; } = null!;
    public MessageType MessageType { get; private set; } = MessageType.Text;
    public decimal? ProposedPrice { get; private set; }
    public DateTime SentAt { get; private set; }
    public DateTime? ReadAt { get; private set; }

    public Conversation Conversation { get; private set; } = null!;

    private Message() { }

    public static Message CreateTextMessage(
        Guid conversationId,
        Guid senderId,
        string content)
    {
        return new Message
        {
            ConversationId = conversationId,
            SenderId = senderId,
            Content = content,
            MessageType = MessageType.Text,
            SentAt = DateTime.UtcNow
        };
    }

    public static Message CreatePriceProposal(
        Guid conversationId,
        Guid senderId,
        decimal proposedPrice,
        string? content = null)
    {
        return new Message
        {
            ConversationId = conversationId,
            SenderId = senderId,
            Content = content ?? $"I propose a price of ₦{proposedPrice:N0}",
            MessageType = MessageType.PriceProposal,
            ProposedPrice = proposedPrice,
            SentAt = DateTime.UtcNow
        };
    }

    public static Message CreatePriceAccepted(
        Guid conversationId,
        Guid senderId,
        decimal acceptedPrice,
        string? content = null)
    {
        return new Message
        {
            ConversationId = conversationId,
            SenderId = senderId,
            Content = content ?? $"Price agreed at ₦{acceptedPrice:N0}",
            MessageType = MessageType.PriceAccepted,
            ProposedPrice = acceptedPrice,
            SentAt = DateTime.UtcNow
        };
    }

    public static Message CreateLockIn(
        Guid conversationId,
        Guid senderId,
        decimal price)
    {
        return new Message
        {
            ConversationId = conversationId,
            SenderId = senderId,
            Content = $"Lock-in proposed at ₦{price:N0}",
            MessageType = MessageType.LockIn,
            ProposedPrice = price,
            SentAt = DateTime.UtcNow
        };
    }

    public static Message CreateLockInConfirmed(
        Guid conversationId,
        Guid senderId,
        decimal price)
    {
        return new Message
        {
            ConversationId = conversationId,
            SenderId = senderId,
            Content = $"Lock-in confirmed at ₦{price:N0}",
            MessageType = MessageType.LockInConfirmed,
            ProposedPrice = price,
            SentAt = DateTime.UtcNow
        };
    }

    public static Message CreateLockInRejected(Guid conversationId, Guid senderId)
    {
        return new Message
        {
            ConversationId = conversationId,
            SenderId = senderId,
            Content = "Lock-in rejected — continue negotiating.",
            MessageType = MessageType.LockInRejected,
            SentAt = DateTime.UtcNow
        };
    }

    public static Message CreateTripStarted(Guid conversationId, Guid senderId)
    {
        return new Message
        {
            ConversationId = conversationId,
            SenderId = senderId,
            Content = "Trip has started.",
            MessageType = MessageType.TripStarted,
            SentAt = DateTime.UtcNow
        };
    }

    public static Message CreateDelivered(Guid conversationId, Guid senderId)
    {
        return new Message
        {
            ConversationId = conversationId,
            SenderId = senderId,
            Content = "Package delivered.",
            MessageType = MessageType.Delivered,
            SentAt = DateTime.UtcNow
        };
    }

    public static Message CreatePriceRejected(
        Guid conversationId,
        Guid senderId,
        decimal? counterPrice = null,
        string? content = null)
    {
        return new Message
        {
            ConversationId = conversationId,
            SenderId = senderId,
            Content = content ?? "I reject this price",
            MessageType = MessageType.PriceRejected,
            ProposedPrice = counterPrice,
            SentAt = DateTime.UtcNow
        };
    }

    public static Message CreateSystemMessage(Guid conversationId, string content)
    {
        return new Message
        {
            ConversationId = conversationId,
            SenderId = Guid.Empty,
            Content = content,
            MessageType = MessageType.System,
            SentAt = DateTime.UtcNow
        };
    }

    public void MarkAsRead()
    {
        if (ReadAt == null)
        {
            ReadAt = DateTime.UtcNow;
            SetUpdated();
        }
    }
}

public enum MessageType
{
    Text,
    PriceProposal,
    PriceAccepted,
    PriceRejected,
    LockIn,
    LockInConfirmed,
    LockInRejected,
    TripStarted,
    Delivered,
    System
}
