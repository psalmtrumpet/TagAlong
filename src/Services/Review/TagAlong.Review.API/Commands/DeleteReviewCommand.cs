using FluentValidation;
using TagAlong.Common.CQRS;
using TagAlong.Common.Results;
using TagAlong.Review.Domain.Repositories;

namespace TagAlong.Review.API.Commands;

public record DeleteReviewCommand(Guid ReviewId, Guid ReviewerId) : ICommand<bool>;

public class DeleteReviewCommandValidator : AbstractValidator<DeleteReviewCommand>
{
    public DeleteReviewCommandValidator()
    {
        RuleFor(x => x.ReviewId).NotEmpty();
        RuleFor(x => x.ReviewerId).NotEmpty();
    }
}

public class DeleteReviewCommandHandler : ICommandHandler<DeleteReviewCommand, bool>
{
    private readonly IReviewRepository _reviewRepository;
    private readonly ILogger<DeleteReviewCommandHandler> _logger;

    public DeleteReviewCommandHandler(
        IReviewRepository reviewRepository,
        ILogger<DeleteReviewCommandHandler> logger)
    {
        _reviewRepository = reviewRepository;
        _logger = logger;
    }

    public async Task<Result<bool>> Handle(DeleteReviewCommand request, CancellationToken cancellationToken)
    {
        var review = await _reviewRepository.GetByIdAsync(request.ReviewId, cancellationToken);
        if (review == null)
        {
            return Result.Failure<bool>(Error.NotFound("Review not found"));
        }

        if (review.ReviewerId != request.ReviewerId)
        {
            return Result.Failure<bool>(Error.Unauthorized("Not authorized to delete this review"));
        }

        _reviewRepository.Delete(review);
        await _reviewRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Review {ReviewId} deleted", review.Id);

        return Result.Success(true);
    }
}
