using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TagAlong.User.API.Commands;
using TagAlong.User.Domain.Repositories;

namespace TagAlong.User.API.Controllers;

[ApiController]
[Route("api/users/kyc")]
public class KycController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IKycVerificationRepository _kycRepo;
    private readonly IUserProfileRepository _profiles;

    public KycController(IMediator mediator, IKycVerificationRepository kycRepo, IUserProfileRepository profiles)
    {
        _mediator = mediator;
        _kycRepo = kycRepo;
        _profiles = profiles;
    }

    /// <summary>
    /// Submit NIN for verification. Calls Dojah NIN API and marks user as verified on success.
    /// </summary>
    [Authorize]
    [HttpPost("verify-nin")]
    public async Task<IActionResult> VerifyNin([FromBody] VerifyNinRequest request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.NIN) || request.NIN.Length != 11)
            return BadRequest(new { error = "NIN must be exactly 11 digits" });

        var command = new VerifyNinCommand(userId.Value, request.NIN.Trim());
        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
            return StatusCode(500, new { error = result.Error.Message });

        return Ok(result.Value);
    }

    /// <summary>
    /// Get current KYC / verification status for the logged-in user.
    /// </summary>
    [Authorize]
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var profile = await _profiles.GetByAuthUserIdAsync(userId.Value, cancellationToken);
        if (profile == null) return NotFound();

        var kyc = await _kycRepo.GetByAuthUserIdAsync(userId.Value, cancellationToken);

        return Ok(new
        {
            isVerified = profile.IsVerified,
            verificationStatus = profile.VerificationStatus.ToString(),
            verifiedAt = profile.VerifiedAt,
            kycStatus = kyc?.Status.ToString(),
            failureReason = kyc?.FailureReason
        });
    }

    /// <summary>
    /// Called by the app after Dojah SDK onSuccess fires. Fetches verification details
    /// from Dojah using the referenceId and marks the user as verified.
    /// </summary>
    [Authorize]
    [HttpPost("confirm-sdk")]
    public async Task<IActionResult> ConfirmSdk([FromBody] ConfirmSdkRequest request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.ReferenceId))
            return BadRequest(new { error = "referenceId is required" });

        var command = new ConfirmSdkVerificationCommand(userId.Value, request.ReferenceId.Trim());
        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
            return StatusCode(500, new { error = result.Error.Message });

        return Ok(result.Value);
    }

    private Guid? GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }
}

public record VerifyNinRequest(string NIN);
public record ConfirmSdkRequest(string ReferenceId);
