using TagAlong.Trip.Domain.Entities;
using TagAlong.Trip.Infrastructure.Persistence;

namespace TagAlong.Trip.API;

/// <summary>Counts paid Google Maps calls per trip (see the google_api_calls table).</summary>
public class GoogleCallLog
{
    private readonly TripDbContext _db;
    private readonly ILogger<GoogleCallLog> _logger;

    public GoogleCallLog(TripDbContext db, ILogger<GoogleCallLog> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task RecordAsync(string kind, bool ok, Guid? userId = null, Guid? tripId = null, Guid? conversationId = null)
    {
        try
        {
            _db.GoogleApiCalls.Add(GoogleApiCall.Create(kind, ok, userId, tripId, conversationId));
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Counting must never break routing
            _logger.LogWarning(ex, "Couldn't record a Google {Kind} call", kind);
        }
    }
}
