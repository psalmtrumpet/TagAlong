namespace TagAlong.Trip.Domain.Entities;

/// <summary>
/// One "arrived" decision — automatic (GPS) or tapped by the driver — with the
/// position and GPS accuracy at the time, so disputes can be checked later.
/// </summary>
public class ArrivalLog
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid? TripId { get; private set; }
    public Guid? ConversationId { get; private set; }
    /// <summary>"auto" or "manual".</summary>
    public string Kind { get; private set; } = "auto";
    /// <summary>"pickup" or "destination".</summary>
    public string Target { get; private set; } = "destination";
    public double Latitude { get; private set; }
    public double Longitude { get; private set; }
    public double? AccuracyMeters { get; private set; }
    public double? SpeedMps { get; private set; }
    public double? DistanceMeters { get; private set; }
    /// <summary>When the phone took the reading.</summary>
    public DateTime? RecordedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private ArrivalLog() { }

    public static ArrivalLog Create(Guid userId, Guid? tripId, Guid? conversationId, string kind, string target,
        double lat, double lng, double? accuracy, double? speed, double? distance, DateTime? recordedAt) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        TripId = tripId,
        ConversationId = conversationId,
        Kind = kind == "manual" ? "manual" : "auto",
        Target = target == "pickup" ? "pickup" : "destination",
        Latitude = lat,
        Longitude = lng,
        AccuracyMeters = accuracy,
        SpeedMps = speed,
        DistanceMeters = distance,
        RecordedAt = recordedAt,
        CreatedAt = DateTime.UtcNow,
    };
}
