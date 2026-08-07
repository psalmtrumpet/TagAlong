using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TagAlong.User.API.Queries;
using TagAlong.User.Domain.Repositories;

namespace TagAlong.User.API.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserProfileRepository _profiles;

    public AdminController(IMediator mediator, IUserProfileRepository profiles)
    {
        _mediator = mediator;
        _profiles = profiles;
    }

    [HttpGet]
    public async Task<IActionResult> ListUsers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string filter = "all",
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new AdminListUsersQuery(page, pageSize, filter.ToLower()), cancellationToken);
        return Ok(result.Value);
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new AdminGetStatsQuery(), cancellationToken);
        return Ok(result.Value);
    }

    [HttpGet("{authUserId:guid}")]
    public async Task<IActionResult> GetUserDetail(Guid authUserId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new AdminGetUserDetailQuery(authUserId), cancellationToken);
        if (result.Value == null)
            return NotFound(new { error = "User not found" });
        return Ok(result.Value);
    }

    [HttpPost("{authUserId:guid}/suspend")]
    public async Task<IActionResult> SuspendUser(Guid authUserId, [FromBody] SuspendUserRequest request, CancellationToken cancellationToken)
    {
        var profile = await _profiles.GetByAuthUserIdAsync(authUserId, cancellationToken);
        if (profile == null)
            return NotFound(new { error = "User not found" });

        profile.Suspend(request.Reason ?? "Suspended by admin");
        _profiles.Update(profile);
        await _profiles.SaveChangesAsync(cancellationToken);

        return Ok(new { message = "User suspended" });
    }

    [HttpPost("{authUserId:guid}/unsuspend")]
    public async Task<IActionResult> UnsuspendUser(Guid authUserId, CancellationToken cancellationToken)
    {
        var profile = await _profiles.GetByAuthUserIdAsync(authUserId, cancellationToken);
        if (profile == null)
            return NotFound(new { error = "User not found" });

        profile.Unsuspend();
        _profiles.Update(profile);
        await _profiles.SaveChangesAsync(cancellationToken);

        return Ok(new { message = "User unsuspended" });
    }
}

public record SuspendUserRequest(string? Reason);
