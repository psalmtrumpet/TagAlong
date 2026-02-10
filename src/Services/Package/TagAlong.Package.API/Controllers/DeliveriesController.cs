using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TagAlong.Package.API.Commands;
using TagAlong.Package.API.DTOs;
using TagAlong.Package.API.Queries;

namespace TagAlong.Package.API.Controllers;

[ApiController]
[Route("api/deliveries")]
[Authorize]
public class DeliveriesController : ControllerBase
{
    private readonly IMediator _mediator;

    public DeliveriesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(DeliveryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDelivery(Guid id, CancellationToken cancellationToken)
    {
        var query = new GetDeliveryByIdQuery(id);
        var result = await _mediator.Send(query, cancellationToken);

        if (result.IsFailure)
        {
            return NotFound(new { error = result.Error.Message });
        }

        return Ok(result.Value);
    }

    [HttpGet("my-deliveries")]
    [ProducesResponseType(typeof(List<DeliveryResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyDeliveries([FromQuery] string role = "sender", CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var query = new GetMyDeliveriesQuery(userId.Value, role);
        var result = await _mediator.Send(query, cancellationToken);

        return Ok(result.Value);
    }

    [HttpPost]
    [ProducesResponseType(typeof(DeliveryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateDelivery([FromBody] CreateDeliveryDto request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var command = new CreateDeliveryCommand(
            userId.Value,
            request.PackageRequestId,
            request.TripId,
            request.AgreedPrice,
            request.MeetupLocation,
            request.MeetupLatitude,
            request.MeetupLongitude,
            request.MeetupTime,
            request.ReceiverName,
            request.ReceiverPhone);

        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return result.Error.Code.Contains("NotFound")
                ? NotFound(new { error = result.Error.Message })
                : BadRequest(new { error = result.Error.Message });
        }

        return CreatedAtAction(nameof(GetDelivery), new { id = result.Value.Id }, result.Value);
    }

    [HttpPut("{id:guid}/status")]
    [ProducesResponseType(typeof(DeliveryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateDeliveryStatus(Guid id, [FromBody] UpdateDeliveryStatusDto request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var command = new UpdateDeliveryStatusCommand(id, userId.Value, request.Status, request.ProofImageUrl);
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
