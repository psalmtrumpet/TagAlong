using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TagAlong.Common.Results;
using TagAlong.Report.API.Commands;
using TagAlong.Report.API.DTOs;
using TagAlong.Report.API.Queries;

namespace TagAlong.Report.API.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ReportsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetReportByIdQuery(id), cancellationToken);
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

    [HttpGet("my-reports")]
    [ProducesResponseType(typeof(IEnumerable<ReportDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyReports(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var result = await _mediator.Send(new GetReportsByReporterQuery(userId.Value, page, pageSize), cancellationToken);
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
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(IEnumerable<ReportDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReportsByUser(
        Guid userId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetReportsByReportedUserQuery(userId, page, pageSize), cancellationToken);
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
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(IEnumerable<ReportDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReportsByDelivery(Guid deliveryId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetReportsByDeliveryQuery(deliveryId), cancellationToken);
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

    [HttpGet("pending")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(IEnumerable<ReportDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPendingReports(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetPendingReportsQuery(page, pageSize), cancellationToken);
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

    [HttpPost("user")]
    [ProducesResponseType(typeof(ReportDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReportUser(
        [FromBody] CreateUserReportRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var command = new CreateUserReportCommand(
            userId.Value,
            request.ReportedUserId,
            request.Reason,
            request.Description);

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

    [HttpPost("delivery")]
    [ProducesResponseType(typeof(ReportDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReportDelivery(
        [FromBody] CreateDeliveryReportRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var command = new CreateDeliveryReportCommand(
            userId.Value,
            request.ReportedDeliveryId,
            request.Reason,
            request.Description);

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

    [HttpPut("{id:guid}/review")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReviewReport(
        Guid id,
        [FromBody] ReviewReportRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var command = new ReviewReportCommand(id, userId.Value, request.AdminNotes);
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

    [HttpPut("{id:guid}/resolve")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResolveReport(
        Guid id,
        [FromBody] ResolveReportRequest request,
        CancellationToken cancellationToken)
    {
        var command = new ResolveReportCommand(id, request.Resolution);
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

    [HttpPut("{id:guid}/reject")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RejectReport(
        Guid id,
        [FromBody] RejectReportRequest request,
        CancellationToken cancellationToken)
    {
        var command = new RejectReportCommand(id, request.Resolution);
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
