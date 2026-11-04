using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TagAlong.Messaging.Domain.Entities;
using TagAlong.Messaging.Infrastructure.Persistence;

namespace TagAlong.Messaging.API.Controllers;

/// <summary>
/// Service-to-service only (not routed by the gateway): passengers on a trip
/// with an agreed price who haven't been picked up yet. While there are any,
/// the driver can't end or cancel the trip (that would cancel their rides).
/// </summary>
[ApiController]
[Route("internal/trips")]
[AllowAnonymous]
public class InternalTripRidesController : ControllerBase
{
    private readonly MessagingDbContext _db;

    public InternalTripRidesController(MessagingDbContext db) => _db = db;

    [HttpGet("{tripId:guid}/awaiting-pickup")]
    public async Task<IActionResult> AwaitingPickup(Guid tripId, CancellationToken ct) =>
        Ok(new
        {
            count = await _db.Conversations.AsNoTracking()
                .CountAsync(c => c.TripId == tripId && c.Status == ConversationStatus.LockedIn, ct),
        });
}
