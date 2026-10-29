using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TagAlong.Trip.Domain.Entities;
using TagAlong.Trip.Infrastructure.Persistence;

namespace TagAlong.Trip.API.Controllers;

/// <summary>Every "arrived" decision (automatic or tapped), for checking disputes.</summary>
[ApiController]
[Authorize]
[Route("api/trips/arrivals")]
public class ArrivalLogsController : ControllerBase
{
    private readonly TripDbContext _db;
    private readonly ILogger<ArrivalLogsController> _logger;

    public ArrivalLogsController(TripDbContext db, ILogger<ArrivalLogsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Log([FromBody] ArrivalLogRequest r, CancellationToken ct)
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(claim, out var userId)) return Unauthorized();

        var entry = ArrivalLog.Create(userId, r.TripId, r.ConversationId, r.Kind, r.Target,
            r.Latitude, r.Longitude, r.AccuracyMeters, r.SpeedMps, r.DistanceMeters,
            r.RecordedAt?.ToUniversalTime());
        _db.ArrivalLogs.Add(entry);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Arrival ({Kind}, {Target}) by {UserId} trip {TripId} ride {ConversationId}: {Distance:F0} m away, accuracy {Accuracy:F0} m",
            entry.Kind, entry.Target, userId, r.TripId, r.ConversationId, r.DistanceMeters, r.AccuracyMeters);
        return Ok();
    }
}

public record ArrivalLogRequest(
    string Kind,
    string Target,
    double Latitude,
    double Longitude,
    Guid? TripId = null,
    Guid? ConversationId = null,
    double? AccuracyMeters = null,
    double? SpeedMps = null,
    double? DistanceMeters = null,
    DateTime? RecordedAt = null);
