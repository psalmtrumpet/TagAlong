using TagAlong.Common.CQRS;
using TagAlong.Common.Results;
using TagAlong.User.API.DTOs;
using TagAlong.User.Domain.Repositories;

namespace TagAlong.User.API.Commands;

public record UpdateProfileCommand(
    Guid AuthUserId,
    string FirstName,
    string LastName,
    string? Bio,
    string? ProfileImageUrl) : ICommand<UserProfileResponse>;

public class UpdateProfileCommandHandler : ICommandHandler<UpdateProfileCommand, UserProfileResponse>
{
    private readonly IUserProfileRepository _userProfileRepository;

    public UpdateProfileCommandHandler(IUserProfileRepository userProfileRepository)
    {
        _userProfileRepository = userProfileRepository;
    }

    public async Task<Result<UserProfileResponse>> Handle(UpdateProfileCommand request, CancellationToken cancellationToken)
    {
        var profile = await _userProfileRepository.GetByAuthUserIdAsync(request.AuthUserId, cancellationToken);

        if (profile == null)
        {
            return Result.Failure<UserProfileResponse>(Error.NotFound($"UserProfile with AuthUserId {request.AuthUserId} not found"));
        }

        profile.UpdateProfile(request.FirstName, request.LastName, request.Bio, request.ProfileImageUrl);

        _userProfileRepository.Update(profile);
        await _userProfileRepository.SaveChangesAsync(cancellationToken);

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
