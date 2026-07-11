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
            request.PageSize,
            request.TripType,
            request.SkipDetourCheck,
            request.MaxDetourSeconds,
            request.DetourTopN);

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

    [HttpGet("traveler/{travelerId:guid}")]
    [ProducesResponseType(typeof(List<TripResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTravelerActiveTrips(Guid travelerId, CancellationToken cancellationToken)
    {
        var query = new GetMyTripsQuery(travelerId);
        var result = await _mediator.Send(query, cancellationToken);

        var activeTrips = result.Value
            .Where(t => t.Status == "InProgress" ||
                        (t.Status == "Scheduled" && t.DepartureTime >= DateTime.UtcNow))
            .OrderBy(t => t.DepartureTime)
            .ToList();

        return Ok(activeTrips);
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
            request.Stops,
            request.PassengerCapacity,
            request.TripType);

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

    [Authorize]
    [HttpPatch("{id:guid}/departure-time")]
    [ProducesResponseType(typeof(TripResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateDepartureTime(Guid id, [FromBody] UpdateDepartureTimeRequest request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var command = new UpdateDepartureTimeCommand(id, userId.Value, request.DepartureTime);
        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return result.Error.Code.Contains("NotFound")
                ? NotFound(new { error = result.Error.Message })
                : BadRequest(new { error = result.Error.Message });
        }

        return Ok(result.Value);
    }

    [Authorize]
    [HttpPut("{id:guid}/location")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateLocation(Guid id, [FromBody] UpdateLocationRequest request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var query = new GetTripByIdQuery(id);
        var tripResult = await _mediator.Send(query, cancellationToken);
        if (tripResult.IsFailure) return NotFound();

        // Only the traveler can update their own location
        if (tripResult.Value.TravelerId != userId.Value) return Forbid();

        var command = new UpdateTripLocationCommand(id, request.Latitude, request.Longitude);
        var result = await _mediator.Send(command, cancellationToken);
        if (result.IsFailure) return BadRequest(new { error = result.Error.Message });

        return Ok(new { latitude = request.Latitude, longitude = request.Longitude, updatedAt = DateTime.UtcNow });
    }

    [HttpGet("{id:guid}/location")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLocation(Guid id, CancellationToken cancellationToken)
    {
        var query = new GetTripByIdQuery(id);
        var result = await _mediator.Send(query, cancellationToken);
        if (result.IsFailure) return NotFound();

        var trip = result.Value;
        return Ok(new
        {
            latitude = trip.CurrentLatitude,
            longitude = trip.CurrentLongitude,
            updatedAt = trip.LocationUpdatedAt
        });
    }

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}

public record UpdateTripStatusRequest(string Status);
public record UpdateDepartureTimeRequest(DateTime DepartureTime);
