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
    public decimal AvailableCapacity { get; private set; }
    public string? VehicleType { get; private set; }
    public string? VehiclePlateNumber { get; private set; }
    public string? Notes { get; private set; }
    public int MaxPackages { get; private set; }
    public int CurrentPackageCount { get; private set; }

    private readonly List<TripStop> _stops = new();
    public IReadOnlyCollection<TripStop> Stops => _stops.AsReadOnly();

    private Trip() { }

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
        int maxPackages = 5)
    {
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
            MaxPackages = maxPackages
        };

        return trip;
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
