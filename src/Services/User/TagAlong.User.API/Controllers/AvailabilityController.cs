using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TagAlong.User.API.Commands;
using TagAlong.User.API.DTOs;
using TagAlong.User.API.Queries;

namespace TagAlong.User.API.Controllers;

[ApiController]
[Route("api/users/availability")]
public class AvailabilityController : ControllerBase
{
    private readonly IMediator _mediator;

    public AvailabilityController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Get current user's availability status
    /// </summary>
    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(AvailabilityResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyAvailability(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var query = new GetAvailabilityStatusQuery(userId.Value);
        var result = await _mediator.Send(query, cancellationToken);

        if (result.IsFailure)
        {
            return NotFound(new { error = result.Error.Message });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Check if a specific user is currently available (public — no auth required)
    /// </summary>
    [HttpGet("{userId:guid}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserAvailability(Guid userId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetUserAvailabilityByIdQuery(userId), cancellationToken);
        return Ok(new { isAvailable = result.Value });
    }

    /// <summary>
    /// Set availability status (on/off with location)
    /// </summary>
    [Authorize]
    [HttpPost("me")]
    [ProducesResponseType(typeof(AvailabilityResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetAvailability([FromBody] SetAvailabilityRequest request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var command = new SetAvailabilityCommand(
            userId.Value,
            request.IsAvailable,
            request.Latitude,
            request.Longitude,
            request.LocationName,
            request.DurationMinutes);

        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error.Code == "NotFound")
                return NotFound(new { error = result.Error.Message });
            return BadRequest(new { error = result.Error.Message });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Update current location (requires availability to be ON)
    /// </summary>
    [Authorize]
    [HttpPut("me/location")]
    [ProducesResponseType(typeof(LocationUpdateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateLocation([FromBody] UpdateLocationRequest request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var command = new UpdateLocationCommand(
            userId.Value,
            request.Latitude,
            request.Longitude,
            request.LocationName);

        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error.Code == "NotFound")
                return NotFound(new { error = result.Error.Message });
            return BadRequest(new { error = result.Error.Message });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Search for available users nearby
    /// </summary>
    [HttpGet("nearby")]
    [ProducesResponseType(typeof(AvailableUsersPagedResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchNearbyUsers(
        [FromQuery] double latitude,
        [FromQuery] double longitude,
        [FromQuery] double? radiusKm,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        var query = new SearchAvailableUsersQuery(
            latitude,
            longitude,
            radiusKm ?? 10.0,
            page ?? 1,
            pageSize ?? 20);

        var result = await _mediator.Send(query, cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Error.Message });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Get count of available users nearby (for UI badge)
    /// </summary>
    [HttpGet("nearby/count")]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetNearbyCount(
        [FromQuery] double latitude,
        [FromQuery] double longitude,
        [FromQuery] double? radiusKm,
        CancellationToken cancellationToken)
    {
        var query = new GetNearbyAvailableCountQuery(
            latitude,
            longitude,
            radiusKm ?? 10.0);

        var result = await _mediator.Send(query, cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Error.Message });
        }

        return Ok(new { count = result.Value });
    }

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}
