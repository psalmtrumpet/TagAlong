using TagAlong.Common.CQRS;
using TagAlong.Common.Results;
using TagAlong.Trip.API.DTOs;
using TagAlong.Trip.Domain.Repositories;

namespace TagAlong.Trip.API.Commands;

public record UpdateDepartureTimeCommand(Guid TripId, Guid TravelerId, DateTime NewDepartureTime) : ICommand<TripResponse>;

public class UpdateDepartureTimeCommandHandler : ICommandHandler<UpdateDepartureTimeCommand, TripResponse>
{
    private readonly ITripRepository _tripRepository;

    public UpdateDepartureTimeCommandHandler(ITripRepository tripRepository)
    {
        _tripRepository = tripRepository;
    }

    public async Task<Result<TripResponse>> Handle(UpdateDepartureTimeCommand request, CancellationToken cancellationToken)
    {
        var trip = await _tripRepository.GetByIdWithStopsAsync(request.TripId, cancellationToken);

        if (trip == null)
            return Result.Failure<TripResponse>(Error.NotFound($"Trip {request.TripId} not found"));

        if (trip.TravelerId != request.TravelerId)
            return Result.Failure<TripResponse>(Error.Unauthorized("Only the trip owner can reschedule"));

        try { trip.RescheduleDeparture(request.NewDepartureTime); }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<TripResponse>(Error.Validation(ex.Message));
        }

        _tripRepository.Update(trip);
        await _tripRepository.SaveChangesAsync(cancellationToken);

        return Result.Success(new TripResponse(
            trip.Id, trip.TravelerId,
            trip.Origin, trip.OriginLatitude, trip.OriginLongitude,
            trip.Destination, trip.DestinationLatitude, trip.DestinationLongitude,
            trip.DepartureTime, trip.EstimatedArrivalTime, trip.ActualArrivalTime,
            trip.Status.ToString(), trip.TripType.ToString(),
            trip.AvailableCapacity, trip.VehicleType, trip.VehiclePlateNumber, trip.Notes,
            trip.MaxPackages, trip.CurrentPackageCount,
            trip.PassengerCapacity, trip.CurrentPassengerCount,
            trip.CurrentLatitude, trip.CurrentLongitude, trip.LocationUpdatedAt,
            trip.Stops.Select(s => new TripStopResponse(s.Id, s.Location, s.Latitude, s.Longitude, s.Order, s.EstimatedTime, s.ActualArrivalTime)).ToList(),
            trip.CreatedAt));
    }
}
