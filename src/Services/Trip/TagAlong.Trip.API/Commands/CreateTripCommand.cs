using Microsoft.Extensions.Logging;
using TagAlong.Common.CQRS;
using TagAlong.Common.Results;
using TagAlong.EventBus;
using TagAlong.Trip.API.DTOs;
using TagAlong.Trip.Domain.Repositories;
using TagAlong.Trip.Infrastructure.Services;

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
    List<TripStopRequest>? Stops,
    int? PassengerCapacity = null,
    string TripType = "Passenger",
    string? RoutePolyline = null,
    int? RouteDurationSeconds = null) : ICommand<TripResponse>;

public class CreateTripCommandHandler : ICommandHandler<CreateTripCommand, TripResponse>
{
    private readonly ITripRepository _tripRepository;
    private readonly IEventBus _eventBus;
    private readonly ITripRouteService _routeService;
    private readonly ILogger<CreateTripCommandHandler> _logger;

    public CreateTripCommandHandler(
        ITripRepository tripRepository,
        IEventBus eventBus,
        ITripRouteService routeService,
        ILogger<CreateTripCommandHandler> logger)
    {
        _tripRepository = tripRepository;
        _eventBus = eventBus;
        _routeService = routeService;
        _logger = logger;
    }

    public async Task<Result<TripResponse>> Handle(CreateTripCommand request, CancellationToken cancellationToken)
    {
        var parsedTripType = Enum.TryParse<Domain.Entities.TripType>(request.TripType, true, out var tt)
            ? tt : Domain.Entities.TripType.Passenger;

        // Block same-time duplicates: prevent two scheduled trips within 30 min of each other
        var existing = await _tripRepository.GetByTravelerIdAsync(request.TravelerId, 1, 100, cancellationToken);
        var duplicate = existing.FirstOrDefault(t =>
            t.Status == Domain.Entities.TripStatus.Scheduled &&
            Math.Abs((t.DepartureTime - request.DepartureTime).TotalMinutes) < 30);
        if (duplicate != null)
            return Result.Failure<TripResponse>(Error.Conflict(
                $"You already have a trip scheduled at that time. Please choose a different departure time."));

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
            request.MaxPackages,
            request.PassengerCapacity,
            parsedTripType);

        if (request.Stops != null)
        {
            foreach (var stop in request.Stops.OrderBy(s => s.Order))
            {
                trip.AddStop(stop.Location, stop.Latitude, stop.Longitude, stop.Order, stop.EstimatedTime);
            }
        }

        // The route the driver picked — the roads they'll actually drive
        var hasChosenRoute = false;
        if (!string.IsNullOrWhiteSpace(request.RoutePolyline))
        {
            try
            {
                trip.SetRoute(PolylineDecoder.Simplify(PolylineDecoder.Decode(request.RoutePolyline), 400),
                    request.RouteDurationSeconds ?? 0);
                hasChosenRoute = true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Ignoring an invalid route polyline for a new trip");
            }
        }

        await _tripRepository.AddAsync(trip, cancellationToken);
        await _tripRepository.SaveChangesAsync(cancellationToken);

        // No route chosen: fetch the fastest one in the background
        if (!hasChosenRoute)
            _ = Task.Run(() => _routeService.FetchAndStoreRouteAsync(trip.Id));

        try
        {
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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish TripCreatedIntegrationEvent for trip {TripId}", trip.Id);
        }

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
            trip.TripType.ToString(),
            trip.AvailableCapacity,
            trip.VehicleType,
            trip.VehiclePlateNumber,
            trip.Notes,
            trip.MaxPackages,
            trip.CurrentPackageCount,
            trip.PassengerCapacity,
            trip.CurrentPassengerCount,
            trip.CurrentLatitude,
            trip.CurrentLongitude,
            trip.LocationUpdatedAt,
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
