using TagAlong.Common.CQRS;
using TagAlong.Common.Results;
using TagAlong.User.API.DTOs;
using TagAlong.User.Domain.Repositories;

namespace TagAlong.User.API.Commands;

public record SetAvailabilityCommand(
    Guid AuthUserId,
    bool IsAvailable,
    double? Latitude,
    double? Longitude,
    string? LocationName,
    int? DurationMinutes) : ICommand<AvailabilityResponse>;

public class SetAvailabilityCommandHandler : ICommandHandler<SetAvailabilityCommand, AvailabilityResponse>
{
    private readonly IUserProfileRepository _userProfileRepository;

    public SetAvailabilityCommandHandler(IUserProfileRepository userProfileRepository)
    {
        _userProfileRepository = userProfileRepository;
    }

    public async Task<Result<AvailabilityResponse>> Handle(SetAvailabilityCommand request, CancellationToken cancellationToken)
    {
        var profile = await _userProfileRepository.GetByAuthUserIdAsync(request.AuthUserId, cancellationToken);

        if (profile == null)
        {
            return Result.Failure<AvailabilityResponse>(Error.NotFound($"UserProfile with AuthUserId {request.AuthUserId} not found"));
        }

        try
        {
            if (request.IsAvailable)
            {
                if (!request.Latitude.HasValue || !request.Longitude.HasValue)
                {
                    return Result.Failure<AvailabilityResponse>(Error.Validation("Latitude and Longitude are required when setting availability"));
                }

                var duration = request.DurationMinutes.HasValue
                    ? TimeSpan.FromMinutes(request.DurationMinutes.Value)
                    : (TimeSpan?)null;

                profile.SetAvailable(
                    request.Latitude.Value,
                    request.Longitude.Value,
                    request.LocationName,
                    duration);
            }
            else
            {
                profile.SetUnavailable();
            }

            _userProfileRepository.Update(profile);
            await _userProfileRepository.SaveChangesAsync(cancellationToken);

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
        catch (InvalidOperationException ex)
        {
            return Result.Failure<AvailabilityResponse>(Error.Validation(ex.Message));
        }
    }
}
