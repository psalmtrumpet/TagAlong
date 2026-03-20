using TagAlong.Common.CQRS;
using TagAlong.Common.Results;
using TagAlong.Trip.Domain.Repositories;

namespace TagAlong.Trip.API.Commands;

public record UpdateTripLocationCommand(Guid TripId, double Latitude, double Longitude) : ICommand<bool>;

public class UpdateTripLocationCommandHandler : ICommandHandler<UpdateTripLocationCommand, bool>
{
    private readonly ITripRepository _tripRepository;

    public UpdateTripLocationCommandHandler(ITripRepository tripRepository)
    {
        _tripRepository = tripRepository;
    }

    public async Task<Result<bool>> Handle(UpdateTripLocationCommand request, CancellationToken cancellationToken)
    {
        var trip = await _tripRepository.GetByIdAsync(request.TripId, cancellationToken);
        if (trip == null)
            return Result.Failure<bool>(new Error("Trip.NotFound", "Trip not found"));

        trip.UpdateLocation(request.Latitude, request.Longitude);
        _tripRepository.Update(trip);
        await _tripRepository.SaveChangesAsync(cancellationToken);

        return Result.Success(true);
    }
}
