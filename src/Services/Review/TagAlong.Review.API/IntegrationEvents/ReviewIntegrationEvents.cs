using TagAlong.EventBus;

namespace TagAlong.Review.API.IntegrationEvents;

public record ReviewCreatedIntegrationEvent(
    Guid ReviewId,
    Guid DeliveryId,
    Guid ReviewerId,
    Guid RevieweeId,
    int Rating,
    double AverageRating,
    int TotalReviews,
    DateTime CreatedAt) : IntegrationEvent;

public record ReviewUpdatedIntegrationEvent(
    Guid ReviewId,
    Guid DeliveryId,
    Guid ReviewerId,
    Guid RevieweeId,
    int NewRating,
    int OldRating,
    double AverageRating,
    int TotalReviews,
    DateTime UpdatedAt) : IntegrationEvent;

public record ReviewDeletedIntegrationEvent(
    Guid ReviewId,
    Guid DeliveryId,
    Guid ReviewerId,
    Guid RevieweeId,
    double AverageRating,
    int TotalReviews,
    DateTime DeletedAt) : IntegrationEvent;
