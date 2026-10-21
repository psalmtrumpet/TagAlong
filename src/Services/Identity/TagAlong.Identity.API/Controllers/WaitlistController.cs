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
        // WhatsApp number is required; email is optional
        var digits = new string((request.Phone ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digits.Length < 10)
            return BadRequest(new { error = "Please enter a valid WhatsApp number." });
        if (!string.IsNullOrWhiteSpace(request.Email) && !request.Email.Contains('@'))
            return BadRequest(new { error = "Please enter a valid email address, or leave it blank." });
        var role = request.Role?.Trim().ToLowerInvariant();
        if (role != null && role != "ride" && role != "drive") role = null;

        var result = await _mediator.Send(
            new JoinWaitlistCommand(request.Email, request.Phone!, request.Area, request.Name, role, request.FromArea, request.ToArea), ct);

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
            e.Area,
            e.Role,
            e.FromArea,
            e.ToArea,
            e.JoinedAt,
        }));
    }
}

// Name is optional (the form now collects email, phone and Lagos area).
public record JoinWaitlistRequest(string? Email = null, string? Phone = null, string? Area = null, string? Name = null,
    string? Role = null, string? FromArea = null, string? ToArea = null);
