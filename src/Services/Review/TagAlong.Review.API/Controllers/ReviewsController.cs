using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TagAlong.Common.Results;
using TagAlong.Review.API.Commands;
using TagAlong.Review.API.DTOs;
using TagAlong.Review.API.Queries;
using TagAlong.Review.Domain.Entities;

namespace TagAlong.Review.API.Controllers;

[ApiController]
[Route("api/reviews")]
[Authorize]
public class ReviewsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ReviewsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ReviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetReviewByIdQuery(id), cancellationToken);
        if (result.IsFailure)
        {
            return result.Error.Type switch
            {
                ErrorType.NotFound => NotFound(new { error = result.Error.Message }),
                ErrorType.Unauthorized => Unauthorized(new { error = result.Error.Message }),
                ErrorType.Conflict => Conflict(new { error = result.Error.Message }),
                _ => BadRequest(new { error = result.Error.Message })
            };
        }
        return Ok(result.Value);
    }

    [HttpGet("user/{userId:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IEnumerable<ReviewDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByUser(
        Guid userId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetReviewsByUserQuery(userId, page, pageSize), cancellationToken);
        if (result.IsFailure)
        {
            return result.Error.Type switch
            {
                ErrorType.NotFound => NotFound(new { error = result.Error.Message }),
                ErrorType.Unauthorized => Unauthorized(new { error = result.Error.Message }),
                ErrorType.Conflict => Conflict(new { error = result.Error.Message }),
                _ => BadRequest(new { error = result.Error.Message })
            };
        }
        return Ok(result.Value);
    }

    [HttpGet("user/{userId:guid}/stats")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ReviewStatsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserStats(Guid userId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetUserReviewStatsQuery(userId), cancellationToken);
        if (result.IsFailure)
        {
            return result.Error.Type switch
            {
                ErrorType.NotFound => NotFound(new { error = result.Error.Message }),
                ErrorType.Unauthorized => Unauthorized(new { error = result.Error.Message }),
                ErrorType.Conflict => Conflict(new { error = result.Error.Message }),
                _ => BadRequest(new { error = result.Error.Message })
            };
        }
        return Ok(result.Value);
    }

    [HttpGet("delivery/{deliveryId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<ReviewDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByDelivery(Guid deliveryId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetReviewsByDeliveryQuery(deliveryId), cancellationToken);
        if (result.IsFailure)
        {
            return result.Error.Type switch
            {
                ErrorType.NotFound => NotFound(new { error = result.Error.Message }),
                ErrorType.Unauthorized => Unauthorized(new { error = result.Error.Message }),
                ErrorType.Conflict => Conflict(new { error = result.Error.Message }),
                _ => BadRequest(new { error = result.Error.Message })
            };
        }
        return Ok(result.Value);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ReviewDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateReview(
        [FromBody] CreateReviewRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        if (!Enum.TryParse<ReviewerRole>(request.ReviewerRole, true, out var reviewerRole))
            return BadRequest(new { error = "Invalid reviewer role. Must be 'Sender' or 'Traveler'" });

        var command = new CreateReviewCommand(
            request.DeliveryId,
            userId.Value,
            request.RevieweeId,
            request.Rating,
            reviewerRole,
            request.Comment);

        var result = await _mediator.Send(command, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error.Type switch
            {
                ErrorType.NotFound => NotFound(new { error = result.Error.Message }),
                ErrorType.Unauthorized => Unauthorized(new { error = result.Error.Message }),
                ErrorType.Conflict => Conflict(new { error = result.Error.Message }),
                _ => BadRequest(new { error = result.Error.Message })
            };
        }
        return CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ReviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateReview(
        Guid id,
        [FromBody] UpdateReviewRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var command = new UpdateReviewCommand(id, userId.Value, request.Rating, request.Comment);
        var result = await _mediator.Send(command, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error.Type switch
            {
                ErrorType.NotFound => NotFound(new { error = result.Error.Message }),
                ErrorType.Unauthorized => Unauthorized(new { error = result.Error.Message }),
                ErrorType.Conflict => Conflict(new { error = result.Error.Message }),
                _ => BadRequest(new { error = result.Error.Message })
            };
        }
        return Ok(result.Value);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteReview(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var command = new DeleteReviewCommand(id, userId.Value);
        var result = await _mediator.Send(command, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error.Type switch
            {
                ErrorType.NotFound => NotFound(new { error = result.Error.Message }),
                ErrorType.Unauthorized => Unauthorized(new { error = result.Error.Message }),
                ErrorType.Conflict => Conflict(new { error = result.Error.Message }),
                _ => BadRequest(new { error = result.Error.Message })
            };
        }
        return NoContent();
    }

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}
