using TagAlong.Common.CQRS;
using TagAlong.Common.Results;
using TagAlong.Review.API.DTOs;
using TagAlong.Review.Domain.Repositories;

namespace TagAlong.Review.API.Queries;

public record GetReviewByIdQuery(Guid Id) : IQuery<ReviewDto?>;
public record GetReviewsByUserQuery(Guid UserId, int Page, int PageSize) : IQuery<IEnumerable<ReviewDto>>;
public record GetReviewsByDeliveryQuery(Guid DeliveryId) : IQuery<IEnumerable<ReviewDto>>;
public record GetUserReviewStatsQuery(Guid UserId) : IQuery<ReviewStatsDto>;

public class GetReviewByIdQueryHandler : IQueryHandler<GetReviewByIdQuery, ReviewDto?>
{
    private readonly IReviewRepository _reviewRepository;

    public GetReviewByIdQueryHandler(IReviewRepository reviewRepository)
    {
        _reviewRepository = reviewRepository;
    }

    public async Task<Result<ReviewDto?>> Handle(GetReviewByIdQuery request, CancellationToken cancellationToken)
    {
        var review = await _reviewRepository.GetByIdAsync(request.Id, cancellationToken);
        if (review == null) return Result.Success<ReviewDto?>(null);

        return Result.Success<ReviewDto?>(new ReviewDto(
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
            review.CreatedAt));
    }
}

public class GetReviewsByUserQueryHandler : IQueryHandler<GetReviewsByUserQuery, IEnumerable<ReviewDto>>
{
    private readonly IReviewRepository _reviewRepository;

    public GetReviewsByUserQueryHandler(IReviewRepository reviewRepository)
    {
        _reviewRepository = reviewRepository;
    }

    public async Task<Result<IEnumerable<ReviewDto>>> Handle(GetReviewsByUserQuery request, CancellationToken cancellationToken)
    {
        var reviews = await _reviewRepository.GetByRevieweeIdAsync(request.UserId, request.Page, request.PageSize, cancellationToken);
        return Result.Success(reviews.Select(r => new ReviewDto(
            r.Id,
            r.DeliveryId,
            r.ReviewerId,
            r.RevieweeId,
            r.Type.ToString(),
            r.Rating,
            r.Comment,
            r.ReviewerRole.ToString(),
            r.IsEdited,
            r.EditedAt,
            r.CreatedAt)));
    }
}

public class GetReviewsByDeliveryQueryHandler : IQueryHandler<GetReviewsByDeliveryQuery, IEnumerable<ReviewDto>>
{
    private readonly IReviewRepository _reviewRepository;

    public GetReviewsByDeliveryQueryHandler(IReviewRepository reviewRepository)
    {
        _reviewRepository = reviewRepository;
    }

    public async Task<Result<IEnumerable<ReviewDto>>> Handle(GetReviewsByDeliveryQuery request, CancellationToken cancellationToken)
    {
        var reviews = await _reviewRepository.GetByDeliveryIdAsync(request.DeliveryId, cancellationToken);
        return Result.Success(reviews.Select(r => new ReviewDto(
            r.Id,
            r.DeliveryId,
            r.ReviewerId,
            r.RevieweeId,
            r.Type.ToString(),
            r.Rating,
            r.Comment,
            r.ReviewerRole.ToString(),
            r.IsEdited,
            r.EditedAt,
            r.CreatedAt)));
    }
}

public class GetUserReviewStatsQueryHandler : IQueryHandler<GetUserReviewStatsQuery, ReviewStatsDto>
{
    private readonly IReviewRepository _reviewRepository;

    public GetUserReviewStatsQueryHandler(IReviewRepository reviewRepository)
    {
        _reviewRepository = reviewRepository;
    }

    public async Task<Result<ReviewStatsDto>> Handle(GetUserReviewStatsQuery request, CancellationToken cancellationToken)
    {
        var averageRating = await _reviewRepository.GetAverageRatingByRevieweeAsync(request.UserId, cancellationToken);
        var reviewCount = await _reviewRepository.GetReviewCountByRevieweeAsync(request.UserId, cancellationToken);
        var distribution = await _reviewRepository.GetRatingDistributionAsync(request.UserId, cancellationToken);

        return Result.Success(new ReviewStatsDto(
            request.UserId,
            averageRating,
            reviewCount,
            distribution));
    }
}
