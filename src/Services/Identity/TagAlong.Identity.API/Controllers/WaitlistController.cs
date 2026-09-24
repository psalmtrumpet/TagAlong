using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TagAlong.Identity.API.Commands;
using TagAlong.Identity.Domain.Repositories;

namespace TagAlong.Identity.API.Controllers;

[ApiController]
public class WaitlistController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IWaitlistRepository _repo;

    public WaitlistController(IMediator mediator, IWaitlistRepository repo)
    {
        _mediator = mediator;
        _repo = repo;
    }

    [HttpPost("api/waitlist")]
    [AllowAnonymous]
    public async Task<IActionResult> Join([FromBody] JoinWaitlistRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length < 2)
            return BadRequest(new { error = "A valid name is required" });
        if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@'))
            return BadRequest(new { error = "A valid email is required" });

        var result = await _mediator.Send(
            new JoinWaitlistCommand(request.Name, request.Email, request.Phone ?? string.Empty), ct);

        return Ok(new { message = result.AlreadyOnList ? "Already on the waitlist" : "Successfully joined the waitlist" });
    }

    [HttpGet("api/admin/waitlist")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var entries = await _repo.GetAllAsync(ct);
        return Ok(entries.Select(e => new
        {
            e.Id,
            e.Name,
            e.Email,
            e.Phone,
            e.JoinedAt,
        }));
    }
}

public record JoinWaitlistRequest(string Name, string Email, string? Phone = null);
