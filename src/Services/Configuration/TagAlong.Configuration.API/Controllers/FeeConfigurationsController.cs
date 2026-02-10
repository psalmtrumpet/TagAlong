using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TagAlong.Common.Results;
using TagAlong.Configuration.API.Commands;
using TagAlong.Configuration.API.DTOs;
using TagAlong.Configuration.API.Queries;

namespace TagAlong.Configuration.API.Controllers;

[ApiController]
[Route("api/fee-configurations")]
public class FeeConfigurationsController : ControllerBase
{
    private readonly IMediator _mediator;

    public FeeConfigurationsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(IEnumerable<FeeConfigurationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var feeConfigurations = await _mediator.Send(new GetAllFeeConfigurationsQuery(page, pageSize), cancellationToken);
        return Ok(feeConfigurations);
    }

    [HttpGet("active")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(FeeConfigurationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetActive(CancellationToken cancellationToken)
    {
        var feeConfiguration = await _mediator.Send(new GetActiveFeeConfigurationQuery(), cancellationToken);
        if (feeConfiguration == null)
            return NotFound();

        return Ok(feeConfiguration);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(FeeConfigurationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var feeConfiguration = await _mediator.Send(new GetFeeConfigurationByIdQuery(id), cancellationToken);
        if (feeConfiguration == null)
            return NotFound();

        return Ok(feeConfiguration);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(FeeConfigurationDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateFeeConfigurationRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateFeeConfigurationCommand(
            request.Name,
            request.MinPercentage,
            request.MaxPercentage,
            request.DefaultPercentage,
            request.Description);

        var result = await _mediator.Send(command, cancellationToken);
        if (result.IsFailure)
            return BadRequest(new { error = result.Error.Message });

        return CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(FeeConfigurationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateFeeConfigurationRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateFeeConfigurationCommand(
            id,
            request.MinPercentage,
            request.MaxPercentage,
            request.DefaultPercentage);

        var feeConfiguration = await _mediator.Send(command, cancellationToken);
        if (feeConfiguration == null)
            return NotFound();

        return Ok(feeConfiguration);
    }

    [HttpPut("{id:guid}/activate")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken cancellationToken)
    {
        var command = new ActivateFeeConfigurationCommand(id);
        var result = await _mediator.Send(command, cancellationToken);
        if (result.IsFailure)
            return NotFound(new { error = result.Error.Message });

        return NoContent();
    }

    [HttpPut("{id:guid}/deactivate")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        var command = new DeactivateFeeConfigurationCommand(id);
        var result = await _mediator.Send(command, cancellationToken);
        if (result.IsFailure)
            return NotFound(new { error = result.Error.Message });

        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var command = new DeleteFeeConfigurationCommand(id);
        var result = await _mediator.Send(command, cancellationToken);
        if (result.IsFailure)
            return NotFound(new { error = result.Error.Message });

        return NoContent();
    }
}
