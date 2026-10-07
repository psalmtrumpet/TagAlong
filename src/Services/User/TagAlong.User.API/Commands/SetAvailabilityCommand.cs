using TagAlong.Common.CQRS;
using TagAlong.Common.Results;
using TagAlong.User.API.DTOs;
using TagAlong.User.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using TagAlong.User.Domain.Entities;
using TagAlong.User.Infrastructure.Persistence;

namespace TagAlong.User.API.Commands;

public record SetAvailabilityCommand(
    Guid AuthUserId,
    bool IsAvailable,
    double? Latitude,
    double? Longitude,
    string? LocationName,
    int? DurationMinutes,
    double? TripDestinationLatitude = null,
    double? TripDestinationLongitude = null,
    string? TripDestinationName = null) : ICommand<AvailabilityResponse>;

public class SetAvailabilityCommandHandler : ICommandHandler<SetAvailabilityCommand, AvailabilityResponse>
{
    private readonly IUserProfileRepository _userProfileRepository;
    private readonly UserDbContext _db;

    public SetAvailabilityCommandHandler(IUserProfileRepository userProfileRepository, UserDbContext db)
    {
        _db = db;
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

                // Taking passengers or packages needs an approved licence + vehicle
                var driverApproved = await _db.DriverProfiles.AsNoTracking().AnyAsync(
                    d => d.AuthUserId == request.AuthUserId && d.Status == DriverProfileStatus.Approved, cancellationToken);
                if (!driverApproved)
                    return Result.Failure<AvailabilityResponse>(Error.Validation(
                        "Your driver's licence and vehicle need to be approved before you can go available."));

                var duration = request.DurationMinutes.HasValue
                    ? TimeSpan.FromMinutes(request.DurationMinutes.Value)
                    : (TimeSpan?)null;

                profile.SetAvailable(
                    request.Latitude.Value,
                    request.Longitude.Value,
                    request.LocationName,
                    request.TripDestinationLatitude,
                    request.TripDestinationLongitude,
                    request.TripDestinationName,
                    duration);
            }
            else
            {
                if (profile.HasOngoingTrip)
                    return Result.Failure<AvailabilityResponse>(Error.Validation("You cannot go offline while you have an active trip in progress."));

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
                profile.AllowLocationSharing,
                profile.HasOngoingTrip));
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<AvailabilityResponse>(Error.Validation(ex.Message));
        }
    }
}
