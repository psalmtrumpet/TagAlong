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
            Content = content ?? $"I propose a price of {proposedPrice:C}",
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
            Content = content ?? $"I accept the price of {acceptedPrice:C}",
            MessageType = MessageType.PriceAccepted,
            ProposedPrice = acceptedPrice,
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
    System
}
