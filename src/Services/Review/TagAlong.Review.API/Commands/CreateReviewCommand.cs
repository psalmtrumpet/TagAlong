using FluentValidation;
using TagAlong.Common.CQRS;
using TagAlong.Common.Results;
using TagAlong.EventBus;
using TagAlong.Review.API.DTOs;
using TagAlong.Review.API.IntegrationEvents;
using TagAlong.Review.Domain.Entities;
using TagAlong.Review.Domain.Repositories;

namespace TagAlong.Review.API.Commands;

public record CreateReviewCommand(
    Guid DeliveryId,
    Guid ReviewerId,
    Guid RevieweeId,
    int Rating,
    ReviewerRole ReviewerRole,
    string? Comment) : ICommand<ReviewDto>;

public class CreateReviewCommandValidator : AbstractValidator<CreateReviewCommand>
{
    public CreateReviewCommandValidator()
    {
        RuleFor(x => x.DeliveryId).NotEmpty();
        RuleFor(x => x.ReviewerId).NotEmpty();
        RuleFor(x => x.RevieweeId).NotEmpty();
        RuleFor(x => x.ReviewerId).NotEqual(x => x.RevieweeId).WithMessage("Cannot review yourself");
        RuleFor(x => x.Rating).InclusiveBetween(1, 5);
        RuleFor(x => x.Comment).MaximumLength(1000).When(x => x.Comment != null);
    }
}

public class CreateReviewCommandHandler : ICommandHandler<CreateReviewCommand, ReviewDto>
{
    private readonly IReviewRepository _reviewRepository;
    private readonly IEventBus _eventBus;
    private readonly ILogger<CreateReviewCommandHandler> _logger;

    public CreateReviewCommandHandler(
        IReviewRepository reviewRepository,
        IEventBus eventBus,
        ILogger<CreateReviewCommandHandler> logger)
    {
        _reviewRepository = reviewRepository;
        _eventBus = eventBus;
        _logger = logger;
    }

    public async Task<Result<ReviewDto>> Handle(CreateReviewCommand request, CancellationToken cancellationToken)
    {
        var existingReview = await _reviewRepository.GetByDeliveryAndReviewerAsync(request.DeliveryId, request.ReviewerId, cancellationToken);
        if (existingReview != null)
        {
            return Result.Failure<ReviewDto>(Error.Conflict("You have already reviewed this delivery"));
        }

        var review = Domain.Entities.Review.Create(
            request.DeliveryId,
            request.ReviewerId,
            request.RevieweeId,
            ReviewType.Delivery,
            request.Rating,
            request.ReviewerRole,
            request.Comment);

        await _reviewRepository.AddAsync(review, cancellationToken);
        await _reviewRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Review {ReviewId} created for delivery {DeliveryId}", review.Id, request.DeliveryId);

        var averageRating = await _reviewRepository.GetAverageRatingByRevieweeAsync(request.RevieweeId, cancellationToken);
        var reviewCount = await _reviewRepository.GetReviewCountByRevieweeAsync(request.RevieweeId, cancellationToken);

        await _eventBus.PublishAsync(new ReviewCreatedIntegrationEvent(
            review.Id,
            review.DeliveryId,
            review.ReviewerId,
            review.RevieweeId,
            review.Rating,
            averageRating,
            reviewCount,
            DateTime.UtcNow));

        return Result.Success(MapToDto(review));
    }

    private static ReviewDto MapToDto(Domain.Entities.Review review) => new(
        review.Id,
        review.DeliveryId,
        review.ReviewerId,
        review.RevieweeId,
        review.Type.ToString(),
        review.Rating,
        review.Comment,
        review.ReviewerRole.ToString(),
        review.IsEdited,
        review.EditedAt,
        review.CreatedAt);
}
