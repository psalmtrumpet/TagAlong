using TagAlong.Common.Domain;

namespace TagAlong.Notification.Domain.Entities;

public class Notification : Entity
{
    public Guid UserId { get; private set; }
    public string Title { get; private set; } = null!;
    public string Message { get; private set; } = null!;
    public NotificationType Type { get; private set; }
    public Guid? ReferenceId { get; private set; }
    public string? ReferenceType { get; private set; }
    public bool IsRead { get; private set; }
    public DateTime? ReadAt { get; private set; }
    public string? Data { get; private set; }

    private Notification() { }

    public static Notification Create(
        Guid userId,
        string title,
        string message,
        NotificationType type,
        Guid? referenceId = null,
        string? referenceType = null,
        string? data = null)
    {
        return new Notification
        {
            UserId = userId,
            Title = title,
            Message = message,
            Type = type,
            ReferenceId = referenceId,
            ReferenceType = referenceType,
            Data = data,
            IsRead = false
        };
    }

    public void MarkAsRead()
    {
        if (!IsRead)
        {
            IsRead = true;
            ReadAt = DateTime.UtcNow;
            SetUpdated();
        }
    }

    public void MarkAsUnread()
    {
        IsRead = false;
        ReadAt = null;
        SetUpdated();
    }
}

public enum NotificationType
{
    TripCreated,
    TripStatusChanged,
    PackageRequestCreated,
    PackageMatched,
    DeliveryStatusChanged,
    DeliveryPickedUp,
    DeliveryCompleted,
    PaymentReceived,
    PaymentPending,
    NewMessage,
    PriceProposal,
    PriceAccepted,
    PriceRejected,
    ReviewReceived,
    ReportSubmitted,
    ReportResolved,
    System
}
