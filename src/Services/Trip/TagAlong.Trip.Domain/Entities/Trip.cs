using NetTopologySuite.Geometries;
using TagAlong.Common.Domain;

namespace TagAlong.Trip.Domain.Entities;

public class Trip : AggregateRoot
{
    public Guid TravelerId { get; private set; }
    public string Origin { get; private set; } = null!;
    public double OriginLatitude { get; private set; }
    public double OriginLongitude { get; private set; }
    public string Destination { get; private set; } = null!;
    public double DestinationLatitude { get; private set; }
    public double DestinationLongitude { get; private set; }
    public DateTime DepartureTime { get; private set; }
    public DateTime? EstimatedArrivalTime { get; private set; }
    public DateTime? ActualArrivalTime { get; private set; }
    public TripStatus Status { get; private set; } = TripStatus.Scheduled;
    public TripType TripType { get; private set; } = TripType.Passenger;
    public decimal AvailableCapacity { get; private set; }
    public string? VehicleType { get; private set; }
    public string? VehiclePlateNumber { get; private set; }
    public string? Notes { get; private set; }
    public int MaxPackages { get; private set; }
    public int CurrentPackageCount { get; private set; }
    public int PassengerCapacity { get; private set; }
    public int CurrentPassengerCount { get; private set; }
    public double? CurrentLatitude { get; private set; }
    public double? CurrentLongitude { get; private set; }
    public DateTime? LocationUpdatedAt { get; private set; }
    public LineString? RouteLine { get; private set; }
    public TripRouteStatus RouteStatus { get; private set; } = TripRouteStatus.None;

    private readonly List<TripStop> _stops = new();
    public IReadOnlyCollection<TripStop> Stops => _stops.AsReadOnly();

    private Trip() { }

    public static int GetMaxPassengers(string? vehicleType) =>
        vehicleType?.ToLower() switch
        {
            "bus" => 12,
            "motorcycle" => 1,
            _ => 3 // Car, SUV, default
        };

    public static Trip Create(
        Guid travelerId,
        string origin,
        double originLatitude,
        double originLongitude,
        string destination,
        double destinationLatitude,
        double destinationLongitude,
        DateTime departureTime,
        DateTime? estimatedArrivalTime,
        decimal availableCapacity,
        string? vehicleType,
        string? vehiclePlateNumber,
        string? notes,
        int maxPackages = 5,
        int? passengerCapacity = null,
        TripType tripType = TripType.Passenger)
    {
        var maxPassengers = GetMaxPassengers(vehicleType);
        var actualPassengerCapacity = passengerCapacity.HasValue
            ? Math.Min(passengerCapacity.Value, maxPassengers)
            : maxPassengers;

        var trip = new Trip
        {
            TravelerId = travelerId,
            Origin = origin,
            OriginLatitude = originLatitude,
            OriginLongitude = originLongitude,
            Destination = destination,
            DestinationLatitude = destinationLatitude,
            DestinationLongitude = destinationLongitude,
            DepartureTime = departureTime,
            EstimatedArrivalTime = estimatedArrivalTime,
            AvailableCapacity = availableCapacity,
            VehicleType = vehicleType,
            VehiclePlateNumber = vehiclePlateNumber,
            Notes = notes,
            MaxPackages = maxPackages,
            PassengerCapacity = actualPassengerCapacity,
            TripType = tripType
        };

        return trip;
    }

    public void UpdateLocation(double latitude, double longitude)
    {
        CurrentLatitude = latitude;
        CurrentLongitude = longitude;
        LocationUpdatedAt = DateTime.UtcNow;
        SetUpdated();
    }

    public bool CanAcceptPassenger() =>
        Status == TripStatus.Scheduled && CurrentPassengerCount < PassengerCapacity;

    public void AddPassenger()
    {
        if (!CanAcceptPassenger())
            throw new InvalidOperationException("No passenger slots available");
        CurrentPassengerCount++;
        SetUpdated();
    }

    public void RemovePassenger()
    {
        if (CurrentPassengerCount > 0)
        {
            CurrentPassengerCount--;
            SetUpdated();
        }
    }

    public void AddStop(string location, double latitude, double longitude, int order, DateTime? estimatedTime = null)
    {
        var stop = TripStop.Create(Id, location, latitude, longitude, order, estimatedTime);
        _stops.Add(stop);
        SetUpdated();
    }

    public void UpdateStops(List<TripStop> stops)
    {
        _stops.Clear();
        _stops.AddRange(stops);
        SetUpdated();
    }

    public void SetRoute(LineString routeLine)
    {
        RouteLine = routeLine;
        RouteStatus = TripRouteStatus.Stored;
        SetUpdated();
    }

    public void MarkRouteStatus(TripRouteStatus status)
    {
        RouteStatus = status;
        SetUpdated();
    }

    public void RescheduleDeparture(DateTime newDepartureTime)
    {
        if (Status != TripStatus.Scheduled)
            throw new InvalidOperationException("Can only reschedule a scheduled trip");

        DepartureTime = newDepartureTime;
        SetUpdated();
    }

    public void Start()
    {
        if (Status != TripStatus.Scheduled)
            throw new InvalidOperationException("Can only start a scheduled trip");

        Status = TripStatus.InProgress;
        SetUpdated();
    }

    public void Complete()
    {
        if (Status != TripStatus.InProgress)
            throw new InvalidOperationException("Can only complete a trip in progress");

        Status = TripStatus.Completed;
        ActualArrivalTime = DateTime.UtcNow;
        SetUpdated();
    }

    public void Cancel()
    {
        if (Status == TripStatus.Completed)
            throw new InvalidOperationException("Cannot cancel a completed trip");

        Status = TripStatus.Cancelled;
        SetUpdated();
    }

    public bool CanAcceptPackage()
    {
        return Status == TripStatus.Scheduled && CurrentPackageCount < MaxPackages;
    }

    public void IncrementPackageCount()
    {
        if (!CanAcceptPackage())
            throw new InvalidOperationException("Cannot accept more packages");

        CurrentPackageCount++;
        SetUpdated();
    }

    public void DecrementPackageCount()
    {
        if (CurrentPackageCount > 0)
        {
            CurrentPackageCount--;
            SetUpdated();
        }
    }

    public bool PassesThrough(double latitude, double longitude, double radiusKm = 10)
    {
        // Check if point is near origin
        if (CalculateDistance(OriginLatitude, OriginLongitude, latitude, longitude) <= radiusKm)
            return true;

        // Check if point is near destination
        if (CalculateDistance(DestinationLatitude, DestinationLongitude, latitude, longitude) <= radiusKm)
            return true;

        // Check if point is near any stop
        foreach (var stop in _stops)
        {
            if (CalculateDistance(stop.Latitude, stop.Longitude, latitude, longitude) <= radiusKm)
                return true;
        }

        return false;
    }

    private static double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371; // Earth's radius in km
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;
}

public enum TripStatus
{
    Scheduled,
    InProgress,
    Completed,
    Cancelled
}

public enum TripType
{
    Passenger = 0,  // Offer a Ride — take people along
    Delivery = 1    // Carry Along — carry packages/goods
}
