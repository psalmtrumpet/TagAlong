using TagAlong.Common.CQRS;
using TagAlong.Common.Results;
using TagAlong.User.API.DTOs;
using TagAlong.User.Domain.Repositories;

namespace TagAlong.User.API.Commands;

public record UpdateLocationCommand(
    Guid AuthUserId,
    double Latitude,
    double Longitude,
    string? LocationName) : ICommand<LocationUpdateResponse>;

public class UpdateLocationCommandHandler : ICommandHandler<UpdateLocationCommand, LocationUpdateResponse>
{
    private readonly IUserProfileRepository _userProfileRepository;

    public UpdateLocationCommandHandler(IUserProfileRepository userProfileRepository)
    {
        _userProfileRepository = userProfileRepository;
    }

    public async Task<Result<LocationUpdateResponse>> Handle(UpdateLocationCommand request, CancellationToken cancellationToken)
    {
        var profile = await _userProfileRepository.GetByAuthUserIdAsync(request.AuthUserId, cancellationToken);

        if (profile == null)
        {
            return Result.Failure<LocationUpdateResponse>(Error.NotFound($"UserProfile with AuthUserId {request.AuthUserId} not found"));
        }

        try
        {
            profile.UpdateLocation(request.Latitude, request.Longitude, request.LocationName);

            _userProfileRepository.Update(profile);
            await _userProfileRepository.SaveChangesAsync(cancellationToken);

            return Result.Success(new LocationUpdateResponse(
                profile.CurrentLatitude!.Value,
                profile.CurrentLongitude!.Value,
                profile.CurrentLocationName,
                profile.LocationUpdatedAt!.Value));
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<LocationUpdateResponse>(Error.Validation(ex.Message));
        }
    }
}
