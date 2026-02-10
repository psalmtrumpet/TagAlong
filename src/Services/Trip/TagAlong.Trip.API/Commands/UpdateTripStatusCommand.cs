using TagAlong.Common.CQRS;
using TagAlong.Common.Results;
using TagAlong.EventBus;
using TagAlong.Trip.API.DTOs;
using TagAlong.Trip.Domain.Entities;
using TagAlong.Trip.Domain.Repositories;

namespace TagAlong.Trip.API.Commands;

public record UpdateTripStatusCommand(Guid TripId, Guid TravelerId, string NewStatus) : ICommand<TripResponse>;

public class UpdateTripStatusCommandHandler : ICommandHandler<UpdateTripStatusCommand, TripResponse>
{
    private readonly ITripRepository _tripRepository;
    private readonly IEventBus _eventBus;

    public UpdateTripStatusCommandHandler(ITripRepository tripRepository, IEventBus eventBus)
    {
        _tripRepository = tripRepository;
        _eventBus = eventBus;
    }

    public async Task<Result<TripResponse>> Handle(UpdateTripStatusCommand request, CancellationToken cancellationToken)
    {
        var trip = await _tripRepository.GetByIdWithStopsAsync(request.TripId, cancellationToken);

        if (trip == null)
        {
            return Result.Failure<TripResponse>(Error.NotFound($"Trip with Id {request.TripId} not found"));
        }

        if (trip.TravelerId != request.TravelerId)
        {
            return Result.Failure<TripResponse>(Error.Unauthorized("You don't have permission to update this trip"));
        }

        var oldStatus = trip.Status.ToString();

        switch (request.NewStatus.ToLower())
        {
            case "start":
            case "inprogress":
                trip.Start();
                break;
            case "complete":
            case "completed":
                trip.Complete();
                break;
            case "cancel":
            case "cancelled":
                trip.Cancel();
                break;
            default:
                return Result.Failure<TripResponse>(Error.Validation($"Invalid status: {request.NewStatus}"));
        }

        _tripRepository.Update(trip);
        await _tripRepository.SaveChangesAsync(cancellationToken);

        await _eventBus.PublishAsync(new TripStatusChangedIntegrationEvent(
            trip.Id,
            trip.TravelerId,
            oldStatus,
            trip.Status.ToString(),
            DateTime.UtcNow), cancellationToken);

        return Result.Success(MapToResponse(trip));
    }

    private static TripResponse MapToResponse(Domain.Entities.Trip trip)
    {
        return new TripResponse(
            trip.Id,
            trip.TravelerId,
            trip.Origin,
            trip.OriginLatitude,
            trip.OriginLongitude,
            trip.Destination,
            trip.DestinationLatitude,
            trip.DestinationLongitude,
            trip.DepartureTime,
            trip.EstimatedArrivalTime,
            trip.ActualArrivalTime,
            trip.Status.ToString(),
            trip.AvailableCapacity,
            trip.VehicleType,
            trip.VehiclePlateNumber,
            trip.Notes,
            trip.MaxPackages,
            trip.CurrentPackageCount,
            trip.Stops.Select(s => new TripStopResponse(
                s.Id,
                s.Location,
                s.Latitude,
                s.Longitude,
                s.Order,
                s.EstimatedTime,
                s.ActualArrivalTime)).ToList(),
            trip.CreatedAt);
    }
}

public record TripStatusChangedIntegrationEvent(
    Guid TripId,
    Guid TravelerId,
    string OldStatus,
    string NewStatus,
    DateTime ChangedAt) : IntegrationEvent;
