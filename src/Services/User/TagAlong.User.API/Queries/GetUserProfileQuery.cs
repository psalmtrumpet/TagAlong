using TagAlong.Common.CQRS;
using TagAlong.Common.Results;
using TagAlong.User.API.DTOs;
using TagAlong.User.Domain.Repositories;

namespace TagAlong.User.API.Queries;

public record GetUserProfileQuery(Guid AuthUserId) : IQuery<UserProfileResponse>;

public class GetUserProfileQueryHandler : IQueryHandler<GetUserProfileQuery, UserProfileResponse>
{
    private readonly IUserProfileRepository _userProfileRepository;

    public GetUserProfileQueryHandler(IUserProfileRepository userProfileRepository)
    {
        _userProfileRepository = userProfileRepository;
    }

    public async Task<Result<UserProfileResponse>> Handle(GetUserProfileQuery request, CancellationToken cancellationToken)
    {
        var profile = await _userProfileRepository.GetByAuthUserIdAsync(request.AuthUserId, cancellationToken);

        if (profile == null)
        {
            return Result.Failure<UserProfileResponse>(Error.NotFound($"UserProfile with AuthUserId {request.AuthUserId} not found"));
        }

        return Result.Success(new UserProfileResponse(
            profile.Id,
            profile.AuthUserId,
            profile.Email,
            profile.FirstName,
            profile.LastName,
            profile.PhoneNumber,
            profile.Bio,
            profile.ProfileImageUrl,
            profile.AverageRating,
            profile.TotalRatings,
            profile.CompletedDeliveries,
            profile.CompletedTrips,
            profile.IsVerified,
            profile.VerificationStatus.ToString(),
            profile.CreatedAt));
    }
}

public record GetPublicProfileQuery(Guid ProfileId) : IQuery<UserPublicProfileResponse>;

public class GetPublicProfileQueryHandler : IQueryHandler<GetPublicProfileQuery, UserPublicProfileResponse>
{
    private readonly IUserProfileRepository _userProfileRepository;

    public GetPublicProfileQueryHandler(IUserProfileRepository userProfileRepository)
    {
        _userProfileRepository = userProfileRepository;
    }

    public async Task<Result<UserPublicProfileResponse>> Handle(GetPublicProfileQuery request, CancellationToken cancellationToken)
    {
        // Try auth user ID first (used by messaging service for name lookup),
        // fall back to profile ID for any direct profile lookups.
        var profile = await _userProfileRepository.GetByAuthUserIdAsync(request.ProfileId, cancellationToken)
                      ?? await _userProfileRepository.GetByIdAsync(request.ProfileId, cancellationToken);

        if (profile == null)
        {
            return Result.Failure<UserPublicProfileResponse>(Error.NotFound($"UserProfile with Id {request.ProfileId} not found"));
        }

        return Result.Success(new UserPublicProfileResponse(
            profile.Id,
            profile.FirstName,
            profile.LastName,
            profile.Bio,
            profile.ProfileImageUrl,
            profile.AverageRating,
            profile.TotalRatings,
            profile.CompletedDeliveries,
            profile.CompletedTrips,
            profile.IsVerified));
    }
}
