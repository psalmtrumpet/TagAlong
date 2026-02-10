using FluentValidation;
using TagAlong.Common.CQRS;
using TagAlong.Common.Results;
using TagAlong.Review.API.DTOs;
using TagAlong.Review.Domain.Repositories;

namespace TagAlong.Review.API.Commands;

public record UpdateReviewCommand(
    Guid ReviewId,
    Guid ReviewerId,
    int Rating,
    string? Comment) : ICommand<ReviewDto>;

public class UpdateReviewCommandValidator : AbstractValidator<UpdateReviewCommand>
{
    public UpdateReviewCommandValidator()
    {
        RuleFor(x => x.ReviewId).NotEmpty();
        RuleFor(x => x.ReviewerId).NotEmpty();
        RuleFor(x => x.Rating).InclusiveBetween(1, 5);
        RuleFor(x => x.Comment).MaximumLength(1000).When(x => x.Comment != null);
    }
}

public class UpdateReviewCommandHandler : ICommandHandler<UpdateReviewCommand, ReviewDto>
{
    private readonly IReviewRepository _reviewRepository;
    private readonly ILogger<UpdateReviewCommandHandler> _logger;

    public UpdateReviewCommandHandler(
        IReviewRepository reviewRepository,
        ILogger<UpdateReviewCommandHandler> logger)
    {
        _reviewRepository = reviewRepository;
        _logger = logger;
    }

    public async Task<Result<ReviewDto>> Handle(UpdateReviewCommand request, CancellationToken cancellationToken)
    {
        var review = await _reviewRepository.GetByIdAsync(request.ReviewId, cancellationToken);
        if (review == null)
        {
            return Result.Failure<ReviewDto>(Error.NotFound("Review not found"));
        }

        if (review.ReviewerId != request.ReviewerId)
        {
            return Result.Failure<ReviewDto>(Error.Unauthorized("Not authorized to update this review"));
        }

        review.UpdateReview(request.Rating, request.Comment);
        _reviewRepository.Update(review);
        await _reviewRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Review {ReviewId} updated", review.Id);

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
