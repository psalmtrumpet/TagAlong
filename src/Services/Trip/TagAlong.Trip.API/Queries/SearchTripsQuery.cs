using TagAlong.Common.CQRS;
using TagAlong.Common.Results;
using TagAlong.Trip.API.DTOs;
using TagAlong.Trip.Domain.Repositories;

namespace TagAlong.Trip.API.Queries;

public record SearchTripsQuery(
    string? Origin,
    string? Destination,
    DateTime? DepartureDate,
    double? OriginLatitude,
    double? OriginLongitude,
    double? DestinationLatitude,
    double? DestinationLongitude,
    double RadiusKm,
    int Page,
    int PageSize,
    string? TripType = null) : IQuery<List<TripResponse>>;

public class SearchTripsQueryHandler : IQueryHandler<SearchTripsQuery, List<TripResponse>>
{
    private readonly ITripRepository _tripRepository;

    public SearchTripsQueryHandler(ITripRepository tripRepository)
    {
        _tripRepository = tripRepository;
    }

    public async Task<Result<List<TripResponse>>> Handle(SearchTripsQuery request, CancellationToken cancellationToken)
    {
        Domain.Entities.TripType? tripTypeFilter = null;
        if (!string.IsNullOrEmpty(request.TripType) &&
            Enum.TryParse<Domain.Entities.TripType>(request.TripType, true, out var parsedType))
            tripTypeFilter = parsedType;

        var trips = await _tripRepository.SearchTripsAsync(
            request.Origin,
            request.Destination,
            request.DepartureDate,
            request.OriginLatitude,
            request.OriginLongitude,
            request.DestinationLatitude,
            request.DestinationLongitude,
            request.RadiusKm,
            request.Page,
            request.PageSize,
            cancellationToken,
            tripTypeFilter);

        return Result.Success(trips.Select(trip => new TripResponse(
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
            trip.CreatedAt)).ToList());
    }
}

public record GetTripByIdQuery(Guid TripId) : IQuery<TripResponse>;

public class GetTripByIdQueryHandler : IQueryHandler<GetTripByIdQuery, TripResponse>
{
    private readonly ITripRepository _tripRepository;

    public GetTripByIdQueryHandler(ITripRepository tripRepository)
    {
        _tripRepository = tripRepository;
    }

    public async Task<Result<TripResponse>> Handle(GetTripByIdQuery request, CancellationToken cancellationToken)
    {
        var trip = await _tripRepository.GetByIdWithStopsAsync(request.TripId, cancellationToken);

        if (trip == null)
        {
            return Result.Failure<TripResponse>(Error.NotFound($"Trip with Id {request.TripId} not found"));
        }

        return Result.Success(new TripResponse(
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
            trip.CreatedAt));
    }
}

public record GetMyTripsQuery(Guid TravelerId) : IQuery<List<TripResponse>>;

public class GetMyTripsQueryHandler : IQueryHandler<GetMyTripsQuery, List<TripResponse>>
{
    private readonly ITripRepository _tripRepository;

    public GetMyTripsQueryHandler(ITripRepository tripRepository)
    {
        _tripRepository = tripRepository;
    }

    public async Task<Result<List<TripResponse>>> Handle(GetMyTripsQuery request, CancellationToken cancellationToken)
    {
        var trips = await _tripRepository.GetByTravelerIdAsync(request.TravelerId, cancellationToken);

        return Result.Success(trips.Select(trip => new TripResponse(
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
            trip.CreatedAt)).ToList());
    }
}
