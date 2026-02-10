using TagAlong.Common.CQRS;
using TagAlong.Common.Results;
using TagAlong.EventBus;
using TagAlong.Trip.API.DTOs;
using TagAlong.Trip.Domain.Repositories;

namespace TagAlong.Trip.API.Commands;

public record CreateTripCommand(
    Guid TravelerId,
    string Origin,
    double OriginLatitude,
    double OriginLongitude,
    string Destination,
    double DestinationLatitude,
    double DestinationLongitude,
    DateTime DepartureTime,
    DateTime? EstimatedArrivalTime,
    decimal AvailableCapacity,
    string? VehicleType,
    string? VehiclePlateNumber,
    string? Notes,
    int MaxPackages,
    List<TripStopRequest>? Stops) : ICommand<TripResponse>;

public class CreateTripCommandHandler : ICommandHandler<CreateTripCommand, TripResponse>
{
    private readonly ITripRepository _tripRepository;
    private readonly IEventBus _eventBus;

    public CreateTripCommandHandler(ITripRepository tripRepository, IEventBus eventBus)
    {
        _tripRepository = tripRepository;
        _eventBus = eventBus;
    }

    public async Task<Result<TripResponse>> Handle(CreateTripCommand request, CancellationToken cancellationToken)
    {
        var trip = Domain.Entities.Trip.Create(
            request.TravelerId,
            request.Origin,
            request.OriginLatitude,
            request.OriginLongitude,
            request.Destination,
            request.DestinationLatitude,
            request.DestinationLongitude,
            request.DepartureTime,
            request.EstimatedArrivalTime,
            request.AvailableCapacity,
            request.VehicleType,
            request.VehiclePlateNumber,
            request.Notes,
            request.MaxPackages);

        if (request.Stops != null)
        {
            foreach (var stop in request.Stops.OrderBy(s => s.Order))
            {
                trip.AddStop(stop.Location, stop.Latitude, stop.Longitude, stop.Order, stop.EstimatedTime);
            }
        }

        await _tripRepository.AddAsync(trip, cancellationToken);
        await _tripRepository.SaveChangesAsync(cancellationToken);

        await _eventBus.PublishAsync(new TripCreatedIntegrationEvent(
            trip.Id,
            trip.TravelerId,
            trip.Origin,
            trip.Destination,
            trip.Stops.Select(s => s.Location).ToList(),
            trip.DepartureTime,
            trip.EstimatedArrivalTime,
            trip.AvailableCapacity,
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

public record TripCreatedIntegrationEvent(
    Guid TripId,
    Guid TravelerId,
    string Origin,
    string Destination,
    List<string> IntermediateStops,
    DateTime DepartureTime,
    DateTime? EstimatedArrivalTime,
    decimal AvailableCapacity,
    DateTime CreatedAt) : IntegrationEvent;
