using TagAlong.Common.CQRS;
using TagAlong.Common.Results;
using TagAlong.User.API.DTOs;
using TagAlong.User.Domain.Repositories;

namespace TagAlong.User.API.Queries;

// Get current user's availability status
public record GetAvailabilityStatusQuery(Guid AuthUserId) : IQuery<AvailabilityResponse>;

public class GetAvailabilityStatusQueryHandler : IQueryHandler<GetAvailabilityStatusQuery, AvailabilityResponse>
{
    private readonly IUserProfileRepository _userProfileRepository;

    public GetAvailabilityStatusQueryHandler(IUserProfileRepository userProfileRepository)
    {
        _userProfileRepository = userProfileRepository;
    }

    public async Task<Result<AvailabilityResponse>> Handle(GetAvailabilityStatusQuery request, CancellationToken cancellationToken)
    {
        var profile = await _userProfileRepository.GetByAuthUserIdAsync(request.AuthUserId, cancellationToken);

        if (profile == null)
        {
            return Result.Failure<AvailabilityResponse>(Error.NotFound($"UserProfile with AuthUserId {request.AuthUserId} not found"));
        }

        // Check if availability has expired
        if (profile.IsAvailable && profile.IsAvailabilityExpired())
        {
            profile.SetUnavailable();
            _userProfileRepository.Update(profile);
            await _userProfileRepository.SaveChangesAsync(cancellationToken);
        }

        return Result.Success(new AvailabilityResponse(
            profile.IsAvailable,
            profile.CurrentLatitude,
            profile.CurrentLongitude,
            profile.CurrentLocationName,
            profile.AvailabilityStartedAt,
            profile.AvailabilityExpiresAt,
            profile.LocationUpdatedAt,
            profile.MaxTravelRadiusKm,
            profile.AllowLocationSharing));
    }
}

// Search for available users nearby
public record SearchAvailableUsersQuery(
    double Latitude,
    double Longitude,
    double RadiusKm,
    int Page,
    int PageSize) : IQuery<AvailableUsersPagedResponse>;

public class SearchAvailableUsersQueryHandler : IQueryHandler<SearchAvailableUsersQuery, AvailableUsersPagedResponse>
{
    private readonly IUserProfileRepository _userProfileRepository;

    public SearchAvailableUsersQueryHandler(IUserProfileRepository userProfileRepository)
    {
        _userProfileRepository = userProfileRepository;
    }

    public async Task<Result<AvailableUsersPagedResponse>> Handle(SearchAvailableUsersQuery request, CancellationToken cancellationToken)
    {
        var users = await _userProfileRepository.SearchAvailableUsersAsync(
            request.Latitude,
            request.Longitude,
            request.RadiusKm,
            request.Page,
            request.PageSize,
            cancellationToken);

        var totalCount = await _userProfileRepository.GetAvailableUsersCountAsync(
            request.Latitude,
            request.Longitude,
            request.RadiusKm,
            cancellationToken);

        var userResponses = users.Select(u => new AvailableUserResponse(
            u.Id,
            u.FirstName,
            u.LastName,
            u.ProfileImageUrl,
            u.AverageRating,
            u.TotalRatings,
            u.CompletedDeliveries,
            u.CompletedTrips,
            u.IsVerified,
            Math.Round(u.DistanceFromKm(request.Latitude, request.Longitude), 2),
            u.CurrentLocationName,
            u.LocationUpdatedAt,
            u.CurrentLatitude,
            u.CurrentLongitude));

        return Result.Success(new AvailableUsersPagedResponse(
            userResponses,
            totalCount,
            request.Page,
            request.PageSize));
    }
}

// Get count of available users nearby (for badge)
public record GetNearbyAvailableCountQuery(
    double Latitude,
    double Longitude,
    double RadiusKm) : IQuery<int>;

public class GetNearbyAvailableCountQueryHandler : IQueryHandler<GetNearbyAvailableCountQuery, int>
{
    private readonly IUserProfileRepository _userProfileRepository;

    public GetNearbyAvailableCountQueryHandler(IUserProfileRepository userProfileRepository)
    {
        _userProfileRepository = userProfileRepository;
    }

    public async Task<Result<int>> Handle(GetNearbyAvailableCountQuery request, CancellationToken cancellationToken)
    {
        var count = await _userProfileRepository.GetAvailableUsersCountAsync(
            request.Latitude,
            request.Longitude,
            request.RadiusKm,
            cancellationToken);

        return Result.Success(count);
    }
}
