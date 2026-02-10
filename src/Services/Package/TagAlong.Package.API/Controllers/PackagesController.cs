using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TagAlong.Package.API.Commands;
using TagAlong.Package.API.DTOs;
using TagAlong.Package.API.Queries;

namespace TagAlong.Package.API.Controllers;

[ApiController]
[Route("api/packages")]
public class PackagesController : ControllerBase
{
    private readonly IMediator _mediator;

    public PackagesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("search")]
    [ProducesResponseType(typeof(List<PackageRequestResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchPackages([FromQuery] SearchPackageRequestsDto request, CancellationToken cancellationToken)
    {
        var query = new SearchPackageRequestsQuery(
            request.PickupLocation,
            request.DeliveryLocation,
            request.PickupLatitude,
            request.PickupLongitude,
            request.DeliveryLatitude,
            request.DeliveryLongitude,
            request.RadiusKm,
            request.Page,
            request.PageSize);

        var result = await _mediator.Send(query, cancellationToken);

        return Ok(result.Value);
    }

    [Authorize]
    [HttpGet("my-packages")]
    [ProducesResponseType(typeof(List<PackageRequestResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyPackages(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var query = new GetMyPackageRequestsQuery(userId.Value);
        var result = await _mediator.Send(query, cancellationToken);

        return Ok(result.Value);
    }

    [Authorize]
    [HttpPost]
    [ProducesResponseType(typeof(PackageRequestResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreatePackageRequest([FromBody] CreatePackageRequestDto request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var command = new CreatePackageRequestCommand(
            userId.Value,
            request.PickupLocation,
            request.PickupLatitude,
            request.PickupLongitude,
            request.DeliveryLocation,
            request.DeliveryLatitude,
            request.DeliveryLongitude,
            request.PackageDescription,
            request.Size,
            request.EstimatedWeight,
            request.OfferedPrice,
            request.SpecialInstructions,
            request.RequiredByDate,
            request.PackageImageUrl);

        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Error.Message });
        }

        return CreatedAtAction(nameof(GetMyPackages), result.Value);
    }

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}
