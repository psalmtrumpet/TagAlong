using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TagAlong.Trip.API.Commands;
using TagAlong.Trip.Domain.Entities;
using TagAlong.Trip.Domain.Repositories;
using TagAlong.Trip.Infrastructure.Persistence;

namespace TagAlong.Trip.API.Controllers;

/// <summary>
/// Service-to-service calls on the internal network only (the gateway and nginx
/// route nothing to /internal).
/// </summary>
[ApiController]
[Route("internal/trips")]
[AllowAnonymous]
public class InternalTripsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ITripRepository _trips;

    public InternalTripsController(IMediator mediator, ITripRepository trips)
    {
        _mediator = mediator;
        _trips = trips;
    }

    /// <summary>End a trip on the driver's behalf (e.g. a private ride's passenger was dropped off).</summary>
    [HttpPost("{id:guid}/complete")]
    public async Task<IActionResult> Complete(Guid id, CancellationToken ct)
    {
        var trip = await _trips.GetByIdAsync(id, ct);
        if (trip == null) return NotFound();
        var result = await _mediator.Send(new UpdateTripStatusCommand(id, trip.TravelerId, "Completed"), ct);
        return result.IsFailure ? BadRequest(new { error = result.Error.Message }) : Ok(result.Value);
    }

    public record BusyDriversRequest(List<Guid> DriverIds);
    public record BusyDriver(Guid DriverId, Guid TripId, bool IsPrivate, string Status);

    /// <summary>
    /// Of these drivers, who is committed to an offered trip: under way, or
    /// leaving within the hour. They don't get on-demand ride requests.
    /// </summary>
    [HttpPost("busy-drivers")]
    public async Task<IActionResult> BusyDrivers(
        [FromBody] BusyDriversRequest body, [FromServices] TripDbContext db, CancellationToken ct)
    {
        if (body.DriverIds is not { Count: > 0 }) return Ok(new List<BusyDriver>());
        var soon = DateTime.UtcNow.AddHours(1);
        var busy = await db.Trips.AsNoTracking()
            .Where(t => body.DriverIds.Contains(t.TravelerId) &&
                        (t.Status == TripStatus.InProgress || (t.Status == TripStatus.Scheduled && t.DepartureTime <= soon)))
            .Select(t => new BusyDriver(t.TravelerId, t.Id, t.IsPrivate, t.Status.ToString()))
            .ToListAsync(ct);
        return Ok(busy);
    }

    /// <summary>Drive time from each origin to one destination (on-demand ride matching).</summary>
    [HttpPost("eta")]
    public async Task<IActionResult> Eta([FromBody] EtaRequest body, [FromServices] EtaService eta, CancellationToken ct) =>
        Ok(new EtaResponse(await eta.GetAsync(body.Origins ?? new List<EtaPoint>(), body.DestLat, body.DestLng, ct)));
}
