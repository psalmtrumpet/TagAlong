using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TagAlong.Common.Results;
using TagAlong.Configuration.API.Commands;
using TagAlong.Configuration.API.DTOs;
using TagAlong.Configuration.API.Queries;

namespace TagAlong.Configuration.API.Controllers;

[ApiController]
[Route("api/configurations")]
public class ConfigurationsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ConfigurationsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(IEnumerable<PlatformConfigurationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var configurations = await _mediator.Send(new GetAllConfigurationsQuery(page, pageSize), cancellationToken);
        return Ok(configurations);
    }

    [HttpGet("active")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IEnumerable<PlatformConfigurationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllActive(CancellationToken cancellationToken)
    {
        var configurations = await _mediator.Send(new GetActiveConfigurationsQuery(), cancellationToken);
        return Ok(configurations);
    }

    [HttpGet("key/{key}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PlatformConfigurationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByKey(string key, CancellationToken cancellationToken)
    {
        var configuration = await _mediator.Send(new GetConfigurationByKeyQuery(key), cancellationToken);
        if (configuration == null)
            return NotFound();

        return Ok(configuration);
    }

    [HttpGet("type/{type}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(IEnumerable<PlatformConfigurationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByType(string type, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<Domain.Entities.ConfigurationType>(type, true, out var configurationType))
            return BadRequest("Invalid configuration type");

        var configurations = await _mediator.Send(new GetConfigurationsByTypeQuery(configurationType), cancellationToken);
        return Ok(configurations);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(PlatformConfigurationDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateConfigurationRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateConfigurationCommand(
            request.Key,
            request.Value,
            request.Description,
            request.Type);

        var result = await _mediator.Send(command, cancellationToken);
        if (result.IsFailure)
            return BadRequest(new { error = result.Error.Message });

        return CreatedAtAction(nameof(GetByKey), new { key = result.Value.Key }, result.Value);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(PlatformConfigurationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateConfigurationRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateConfigurationCommand(id, request.Value);
        var configuration = await _mediator.Send(command, cancellationToken);
        if (configuration == null)
            return NotFound();

        return Ok(configuration);
    }

    [HttpPut("{id:guid}/activate")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken cancellationToken)
    {
        var command = new ActivateConfigurationCommand(id);
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
        var command = new DeactivateConfigurationCommand(id);
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
        var command = new DeleteConfigurationCommand(id);
        var result = await _mediator.Send(command, cancellationToken);
        if (result.IsFailure)
            return NotFound(new { error = result.Error.Message });

        return NoContent();
    }
}
