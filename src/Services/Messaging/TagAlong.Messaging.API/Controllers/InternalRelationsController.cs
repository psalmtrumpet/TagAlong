using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TagAlong.Messaging.Infrastructure.Persistence;

namespace TagAlong.Messaging.API.Controllers;

/// <summary>
/// Service-to-service only (not routed by the gateway): whether two people
/// have a ride or a ride request between them — the user service shows a
/// verification selfie only to people who do.
/// </summary>
[ApiController]
[Route("internal/relations")]
[AllowAnonymous]
public class InternalRelationsController : ControllerBase
{
    /// <summary>A "Ride now" request counts this long after it was made.</summary>
    private static readonly TimeSpan HailWindow = TimeSpan.FromHours(24);

    private readonly MessagingDbContext _db;

    public InternalRelationsController(MessagingDbContext db) => _db = db;

    [HttpGet("{a:guid}/{b:guid}")]
    public async Task<IActionResult> Get(Guid a, Guid b, CancellationToken ct)
    {
        // A ride, or a request to join one, in any state (past rides keep their photos)
        var ride = await _db.Conversations.AsNoTracking().AnyAsync(c =>
            (c.SenderId == a && c.TravelerId == b) || (c.SenderId == b && c.TravelerId == a) ||
            (c.RecipientUserId == a && (c.SenderId == b || c.TravelerId == b)) ||
            (c.RecipientUserId == b && (c.SenderId == a || c.TravelerId == a)), ct);
        if (ride) return Ok(new { related = true });

        // A driver who was offered this passenger's "Ride now" request
        var since = DateTime.UtcNow - HailWindow;
        var hail = await (from c in _db.HailCandidates.AsNoTracking()
                          join h in _db.HailRequests.AsNoTracking() on c.HailId equals h.Id
                          where h.CreatedAt > since &&
                                ((h.PassengerId == a && c.DriverId == b) || (h.PassengerId == b && c.DriverId == a))
                          select c.Id).AnyAsync(ct);
        return Ok(new { related = hail });
    }
}
