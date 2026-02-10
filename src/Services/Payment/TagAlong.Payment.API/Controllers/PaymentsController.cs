using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TagAlong.Common.Results;
using TagAlong.Payment.API.Commands;
using TagAlong.Payment.API.DTOs;
using TagAlong.Payment.API.Queries;

namespace TagAlong.Payment.API.Controllers;

[ApiController]
[Route("api/payments")]
[Authorize]
public class PaymentsController : ControllerBase
{
    private readonly IMediator _mediator;

    public PaymentsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PaymentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetPaymentByIdQuery(id), cancellationToken);
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

        var userId = GetCurrentUserId();
        if (userId == null || (result.Value.SenderId != userId && result.Value.TravelerId != userId))
            return Forbid();

        return Ok(result.Value);
    }

    [HttpGet("delivery/{deliveryId:guid}")]
    [ProducesResponseType(typeof(PaymentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByDeliveryId(Guid deliveryId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetPaymentByDeliveryIdQuery(deliveryId), cancellationToken);
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

        var userId = GetCurrentUserId();
        if (userId == null || (result.Value.SenderId != userId && result.Value.TravelerId != userId))
            return Forbid();

        return Ok(result.Value);
    }

    [HttpGet("my-payments")]
    [ProducesResponseType(typeof(IEnumerable<PaymentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyPayments(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var senderResult = await _mediator.Send(new GetPaymentsBySenderQuery(userId.Value, page, pageSize), cancellationToken);
        var travelerResult = await _mediator.Send(new GetPaymentsByTravelerQuery(userId.Value, page, pageSize), cancellationToken);

        if (senderResult.IsFailure)
        {
            return senderResult.Error.Type switch
            {
                ErrorType.NotFound => NotFound(new { error = senderResult.Error.Message }),
                ErrorType.Unauthorized => Unauthorized(new { error = senderResult.Error.Message }),
                ErrorType.Conflict => Conflict(new { error = senderResult.Error.Message }),
                _ => BadRequest(new { error = senderResult.Error.Message })
            };
        }

        if (travelerResult.IsFailure)
        {
            return travelerResult.Error.Type switch
            {
                ErrorType.NotFound => NotFound(new { error = travelerResult.Error.Message }),
                ErrorType.Unauthorized => Unauthorized(new { error = travelerResult.Error.Message }),
                ErrorType.Conflict => Conflict(new { error = travelerResult.Error.Message }),
                _ => BadRequest(new { error = travelerResult.Error.Message })
            };
        }

        var allPayments = senderResult.Value.Concat(travelerResult.Value)
            .OrderByDescending(p => p.CreatedAt)
            .Take(pageSize);

        return Ok(allPayments);
    }

    [HttpGet("sender/{senderId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<PaymentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBySender(
        Guid senderId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (userId == null || userId != senderId) return Forbid();

        var result = await _mediator.Send(new GetPaymentsBySenderQuery(senderId, page, pageSize), cancellationToken);
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

    [HttpGet("traveler/{travelerId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<PaymentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByTraveler(
        Guid travelerId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (userId == null || userId != travelerId) return Forbid();

        var result = await _mediator.Send(new GetPaymentsByTravelerQuery(travelerId, page, pageSize), cancellationToken);
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

    [HttpGet("earnings")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyEarnings(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var result = await _mediator.Send(new GetTravelerEarningsQuery(userId.Value), cancellationToken);
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
        return Ok(new { totalEarnings = result.Value });
    }

    [HttpGet("spending")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMySpending(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var result = await _mediator.Send(new GetSenderSpendingQuery(userId.Value), cancellationToken);
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
        return Ok(new { totalSpent = result.Value });
    }

    [HttpPost]
    [ProducesResponseType(typeof(PaymentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> InitiatePayment(
        [FromBody] InitiatePaymentRequest request,
        [FromServices] IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        if (userId != request.SenderId)
            return Forbid();

        var platformFeePercentage = configuration.GetValue<decimal>("PlatformFeePercentage", 10m);

        var command = new InitiatePaymentCommand(
            request.DeliveryId,
            request.SenderId,
            request.TravelerId,
            request.Amount,
            platformFeePercentage,
            request.PaymentMethod,
            request.PaymentProvider);

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

    [HttpPost("{id:guid}/confirm")]
    [ProducesResponseType(typeof(PaymentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ConfirmPayment(
        Guid id,
        [FromBody] ConfirmPaymentRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var existingResult = await _mediator.Send(new GetPaymentByIdQuery(id), cancellationToken);
        if (existingResult.IsFailure)
        {
            return existingResult.Error.Type switch
            {
                ErrorType.NotFound => NotFound(new { error = existingResult.Error.Message }),
                ErrorType.Unauthorized => Unauthorized(new { error = existingResult.Error.Message }),
                ErrorType.Conflict => Conflict(new { error = existingResult.Error.Message }),
                _ => BadRequest(new { error = existingResult.Error.Message })
            };
        }

        if (existingResult.Value.SenderId != userId)
            return Forbid();

        var command = new ConfirmPaymentCommand(id, request.TransactionReference);
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

    [HttpPost("{id:guid}/refund")]
    [ProducesResponseType(typeof(PaymentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RefundPayment(
        Guid id,
        [FromBody] RefundPaymentRequest request,
        CancellationToken cancellationToken)
    {
        var existingResult = await _mediator.Send(new GetPaymentByIdQuery(id), cancellationToken);
        if (existingResult.IsFailure)
        {
            return existingResult.Error.Type switch
            {
                ErrorType.NotFound => NotFound(new { error = existingResult.Error.Message }),
                ErrorType.Unauthorized => Unauthorized(new { error = existingResult.Error.Message }),
                ErrorType.Conflict => Conflict(new { error = existingResult.Error.Message }),
                _ => BadRequest(new { error = existingResult.Error.Message })
            };
        }

        var command = new RefundPaymentCommand(id, request.Reason);
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

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}
