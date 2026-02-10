namespace TagAlong.Review.API.DTOs;

public record ReviewDto(
    Guid Id,
    Guid DeliveryId,
    Guid ReviewerId,
    Guid RevieweeId,
    string Type,
    int Rating,
    string? Comment,
    string ReviewerRole,
    bool IsEdited,
    DateTime? EditedAt,
    DateTime CreatedAt);

public record ReviewSummaryDto(
    Guid UserId,
    double AverageRating,
    int TotalReviews,
    Dictionary<int, int> RatingDistribution);

public record CreateReviewRequest(
    Guid DeliveryId,
    Guid RevieweeId,
    int Rating,
    string ReviewerRole,
    string? Comment);

public record UpdateReviewRequest(
    int Rating,
    string? Comment);

public record ReviewStatsDto(
    Guid UserId,
    double AverageRating,
    int TotalReviews,
    Dictionary<int, int> RatingDistribution);
