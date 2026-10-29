namespace TagAlong.Trip.Domain.Entities;

/// <summary>
/// One paid Google Maps call (route, live route refresh, drive times), with the
/// trip/ride it was for, so the cost per trip can be counted.
/// </summary>
public class GoogleApiCall
{
    public Guid Id { get; private set; }
    public Guid? UserId { get; private set; }
    public Guid? TripId { get; private set; }
    public Guid? ConversationId { get; private set; }
    /// <summary>"live-route", "route-options" or "eta".</summary>
    public string Kind { get; private set; } = "live-route";
    /// <summary>Google answered (false: it failed and nothing useful came back).</summary>
    public bool Ok { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private GoogleApiCall() { }

    public static GoogleApiCall Create(string kind, bool ok, Guid? userId = null, Guid? tripId = null, Guid? conversationId = null) => new()
    {
        Id = Guid.NewGuid(),
        Kind = kind.Length > 20 ? kind[..20] : kind,
        Ok = ok,
        UserId = userId,
        TripId = tripId,
        ConversationId = conversationId,
        CreatedAt = DateTime.UtcNow,
    };
}
