using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TagAlong.User.API.Commands;
using TagAlong.User.API.DTOs;
using TagAlong.User.API.Queries;
using TagAlong.User.Domain.Entities;
using TagAlong.User.Domain.Repositories;

namespace TagAlong.User.API.Controllers;

[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserProfileRepository _profiles;
    private readonly IKycVerificationRepository _kycRepo;
    private readonly ILogger<UsersController> _logger;

    public UsersController(IMediator mediator, IUserProfileRepository profiles,
        IKycVerificationRepository kycRepo, ILogger<UsersController> logger)
    {
        _mediator = mediator;
        _profiles = profiles;
        _kycRepo = kycRepo;
        _logger = logger;
    }

    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyProfile(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var query = new GetUserProfileQuery(userId.Value);
        var result = await _mediator.Send(query, cancellationToken);

        if (result.IsFailure)
        {
            return NotFound(new { error = result.Error.Message });
        }

        var profile = result.Value;

        // Self-heal: if profile is stuck in Pending with no active KYC record, reset it.
        if (profile.VerificationStatus == "Pending" && !profile.IsVerified)
        {
            var kyc = await _kycRepo.GetByAuthUserIdAsync(userId.Value, cancellationToken);
            if (kyc == null)
            {
                var entity = await _profiles.GetByAuthUserIdAsync(userId.Value, cancellationToken);
                if (entity != null)
                {
                    entity.ResetVerificationStatus();
                    _profiles.Update(entity);
                    await _profiles.SaveChangesAsync(cancellationToken);
                    _logger.LogInformation("Self-healed stale Pending on /users/me for user {UserId}", userId);
                    profile = profile with { VerificationStatus = "None" };
                }
            }
        }

        return Ok(profile);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(UserPublicProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPublicProfile(Guid id, CancellationToken cancellationToken)
    {
        var query = new GetPublicProfileQuery(id);
        var result = await _mediator.Send(query, cancellationToken);

        if (result.IsFailure)
        {
            return NotFound(new { error = result.Error.Message });
        }

        return Ok(result.Value);
    }

    [Authorize]
    [HttpPut("me")]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var command = new UpdateProfileCommand(
            userId.Value,
            request.FirstName,
            request.LastName,
            request.Bio,
            request.ProfileImageUrl);

        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return NotFound(new { error = result.Error.Message });
        }

        return Ok(result.Value);
    }

    [Authorize]
    [HttpGet("search")]
    [ProducesResponseType(typeof(IEnumerable<UserSearchResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchUsers([FromQuery] string q, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 3)
            return Ok(Array.Empty<UserSearchResultDto>());

        var result = await _mediator.Send(new SearchUsersQuery(q), cancellationToken);
        return Ok(result.Value);
    }

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}
