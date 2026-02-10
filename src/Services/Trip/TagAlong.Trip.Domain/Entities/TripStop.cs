using TagAlong.Common.Domain;

namespace TagAlong.Trip.Domain.Entities;

public class TripStop : Entity
{
    public Guid TripId { get; private set; }
    public string Location { get; private set; } = null!;
    public double Latitude { get; private set; }
    public double Longitude { get; private set; }
    public int Order { get; private set; }
    public DateTime? EstimatedTime { get; private set; }
    public DateTime? ActualArrivalTime { get; private set; }

    private TripStop() { }

    public static TripStop Create(
        Guid tripId,
        string location,
        double latitude,
        double longitude,
        int order,
        DateTime? estimatedTime = null)
    {
        return new TripStop
        {
            TripId = tripId,
            Location = location,
            Latitude = latitude,
            Longitude = longitude,
            Order = order,
            EstimatedTime = estimatedTime
        };
    }

    public void MarkArrived()
    {
        ActualArrivalTime = DateTime.UtcNow;
        SetUpdated();
    }
}
