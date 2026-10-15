using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TagAlong.Trip.API.Commands;
using TagAlong.Trip.Domain.Repositories;

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
}
