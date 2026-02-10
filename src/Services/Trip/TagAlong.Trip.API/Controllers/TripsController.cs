using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TagAlong.Trip.API.Commands;
using TagAlong.Trip.API.DTOs;
using TagAlong.Trip.API.Queries;

namespace TagAlong.Trip.API.Controllers;

[ApiController]
[Route("api/trips")]
public class TripsController : ControllerBase
{
    private readonly IMediator _mediator;

    public TripsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("search")]
    [ProducesResponseType(typeof(List<TripResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchTrips([FromQuery] SearchTripsRequest request, CancellationToken cancellationToken)
    {
        var query = new SearchTripsQuery(
            request.Origin,
            request.Destination,
            request.DepartureDate,
            request.OriginLatitude,
            request.OriginLongitude,
            request.DestinationLatitude,
            request.DestinationLongitude,
            request.RadiusKm,
            request.Page,
            request.PageSize);

        var result = await _mediator.Send(query, cancellationToken);

        return Ok(result.Value);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(TripResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTrip(Guid id, CancellationToken cancellationToken)
    {
        var query = new GetTripByIdQuery(id);
        var result = await _mediator.Send(query, cancellationToken);

        if (result.IsFailure)
        {
            return NotFound(new { error = result.Error.Message });
        }

        return Ok(result.Value);
    }

    [Authorize]
    [HttpGet("my-trips")]
    [ProducesResponseType(typeof(List<TripResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyTrips(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var query = new GetMyTripsQuery(userId.Value);
        var result = await _mediator.Send(query, cancellationToken);

        return Ok(result.Value);
    }

    [Authorize]
    [HttpPost]
    [ProducesResponseType(typeof(TripResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateTrip([FromBody] CreateTripRequest request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var command = new CreateTripCommand(
            userId.Value,
            request.Origin,
            request.OriginLatitude,
            request.OriginLongitude,
            request.Destination,
            request.DestinationLatitude,
            request.DestinationLongitude,
            request.DepartureTime,
            request.EstimatedArrivalTime,
            request.AvailableCapacity,
            request.VehicleType,
            request.VehiclePlateNumber,
            request.Notes,
            request.MaxPackages,
            request.Stops);

        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Error.Message });
        }

        return CreatedAtAction(nameof(GetTrip), new { id = result.Value.Id }, result.Value);
    }

    [Authorize]
    [HttpPut("{id:guid}/status")]
    [ProducesResponseType(typeof(TripResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateTripStatus(Guid id, [FromBody] UpdateTripStatusRequest request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var command = new UpdateTripStatusCommand(id, userId.Value, request.Status);
        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return result.Error.Code.Contains("NotFound")
                ? NotFound(new { error = result.Error.Message })
                : BadRequest(new { error = result.Error.Message });
        }

        return Ok(result.Value);
    }

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}

public record UpdateTripStatusRequest(string Status);
