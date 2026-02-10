using TagAlong.Common.Domain;

namespace TagAlong.Review.Domain.Entities;

public class Review : AggregateRoot
{
    public Guid DeliveryId { get; private set; }
    public Guid ReviewerId { get; private set; }
    public Guid RevieweeId { get; private set; }
    public ReviewType Type { get; private set; }
    public int Rating { get; private set; }
    public string? Comment { get; private set; }
    public ReviewerRole ReviewerRole { get; private set; }
    public bool IsEdited { get; private set; }
    public DateTime? EditedAt { get; private set; }

    private Review() { }

    public static Review Create(
        Guid deliveryId,
        Guid reviewerId,
        Guid revieweeId,
        ReviewType type,
        int rating,
        ReviewerRole reviewerRole,
        string? comment = null)
    {
        if (rating < 1 || rating > 5)
            throw new ArgumentException("Rating must be between 1 and 5");

        return new Review
        {
            DeliveryId = deliveryId,
            ReviewerId = reviewerId,
            RevieweeId = revieweeId,
            Type = type,
            Rating = rating,
            ReviewerRole = reviewerRole,
            Comment = comment
        };
    }

    public void UpdateReview(int rating, string? comment)
    {
        if (rating < 1 || rating > 5)
            throw new ArgumentException("Rating must be between 1 and 5");

        Rating = rating;
        Comment = comment;
        IsEdited = true;
        EditedAt = DateTime.UtcNow;
        SetUpdated();
    }
}

public enum ReviewType
{
    Delivery,
    User
}

public enum ReviewerRole
{
    Sender,
    Traveler
}
