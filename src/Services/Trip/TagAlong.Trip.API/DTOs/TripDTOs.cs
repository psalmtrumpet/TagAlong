namespace TagAlong.Trip.API.DTOs;

public record CreateTripRequest(
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
    int? PassengerCapacity,
    List<TripStopRequest>? Stops,
    string TripType = "Passenger");

public record TripStopRequest(
    string Location,
    double Latitude,
    double Longitude,
    int Order,
    DateTime? EstimatedTime);

public record UpdateTripRequest(
    DateTime DepartureTime,
    DateTime? EstimatedArrivalTime,
    decimal AvailableCapacity,
    string? VehicleType,
    string? VehiclePlateNumber,
    string? Notes,
    int MaxPackages,
    List<TripStopRequest>? Stops);

public record UpdateLocationRequest(double Latitude, double Longitude);

public record TripResponse(
    Guid Id,
    Guid TravelerId,
    string Origin,
    double OriginLatitude,
    double OriginLongitude,
    string Destination,
    double DestinationLatitude,
    double DestinationLongitude,
    DateTime DepartureTime,
    DateTime? EstimatedArrivalTime,
    DateTime? ActualArrivalTime,
    string Status,
    string TripType,
    decimal AvailableCapacity,
    string? VehicleType,
    string? VehiclePlateNumber,
    string? Notes,
    int MaxPackages,
    int CurrentPackageCount,
    int PassengerCapacity,
    int CurrentPassengerCount,
    double? CurrentLatitude,
    double? CurrentLongitude,
    DateTime? LocationUpdatedAt,
    List<TripStopResponse> Stops,
    DateTime CreatedAt);

public record TripStopResponse(
    Guid Id,
    string Location,
    double Latitude,
    double Longitude,
    int Order,
    DateTime? EstimatedTime,
    DateTime? ActualArrivalTime);

public record SearchTripsRequest(
    string? Origin,
    string? Destination,
    DateTime? DepartureDate,
    double? OriginLatitude,
    double? OriginLongitude,
    double? DestinationLatitude,
    double? DestinationLongitude,
    double RadiusKm = 10,
    int Page = 1,
    int PageSize = 20,
    string? TripType = null,
    bool SkipDetourCheck = false,
    int MaxDetourSeconds = 600,
    int DetourTopN = 10);
