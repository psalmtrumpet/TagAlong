using TagAlong.EventBus;
using TagAlong.User.Domain.Repositories;

namespace TagAlong.User.API.IntegrationEvents;

// Mirrors of the Review service events (routed by type name).
public record ReviewCreatedIntegrationEvent(
    Guid ReviewId, Guid DeliveryId, Guid ReviewerId, Guid RevieweeId,
    int Rating, double AverageRating, int TotalReviews, DateTime CreatedAt) : IntegrationEvent;

public record ReviewUpdatedIntegrationEvent(
    Guid ReviewId, Guid DeliveryId, Guid ReviewerId, Guid RevieweeId,
    int NewRating, int OldRating, double AverageRating, int TotalReviews, DateTime UpdatedAt) : IntegrationEvent;

public record ReviewDeletedIntegrationEvent(
    Guid ReviewId, Guid DeliveryId, Guid ReviewerId, Guid RevieweeId,
    double AverageRating, int TotalReviews, DateTime DeletedAt) : IntegrationEvent;

/// <summary>Keeps UserProfile.AverageRating / TotalRatings in step with the Review service.</summary>
public class ReviewRatingSyncHandler :
    IIntegrationEventHandler<ReviewCreatedIntegrationEvent>,
    IIntegrationEventHandler<ReviewUpdatedIntegrationEvent>,
    IIntegrationEventHandler<ReviewDeletedIntegrationEvent>
{
    private readonly IUserProfileRepository _userProfileRepository;
    private readonly ILogger<ReviewRatingSyncHandler> _logger;

    public ReviewRatingSyncHandler(IUserProfileRepository userProfileRepository, ILogger<ReviewRatingSyncHandler> logger)
    {
        _userProfileRepository = userProfileRepository;
        _logger = logger;
    }

    public Task HandleAsync(ReviewCreatedIntegrationEvent e, CancellationToken cancellationToken = default) =>
        SyncAsync(e.RevieweeId, e.AverageRating, e.TotalReviews, cancellationToken);

    public Task HandleAsync(ReviewUpdatedIntegrationEvent e, CancellationToken cancellationToken = default) =>
        SyncAsync(e.RevieweeId, e.AverageRating, e.TotalReviews, cancellationToken);

    public Task HandleAsync(ReviewDeletedIntegrationEvent e, CancellationToken cancellationToken = default) =>
        SyncAsync(e.RevieweeId, e.AverageRating, e.TotalReviews, cancellationToken);

    private async Task SyncAsync(Guid userId, double average, int total, CancellationToken cancellationToken)
    {
        var profile = await _userProfileRepository.GetByAuthUserIdAsync(userId, cancellationToken);
        if (profile == null) return;

        profile.SetRatingSummary(average, total);
        _userProfileRepository.Update(profile);
        await _userProfileRepository.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Rating for {UserId} now {Average:0.00} from {Total} review(s)", userId, average, total);
    }
}
