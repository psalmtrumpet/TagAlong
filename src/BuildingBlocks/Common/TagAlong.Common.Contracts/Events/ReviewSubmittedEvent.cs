namespace TagAlong.Common.Contracts.Events;

public record ReviewSubmittedEvent(
    Guid ReviewId,
    Guid DeliveryId,
    Guid ReviewerId,
    Guid RevieweeId,
    int Rating,
    string? Comment,
    DateTime SubmittedAt);
