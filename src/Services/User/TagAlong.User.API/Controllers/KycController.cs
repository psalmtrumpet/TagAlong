using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TagAlong.User.API.Commands;
using TagAlong.User.API.Services;
using TagAlong.User.Domain.Entities;
using TagAlong.User.Domain.Repositories;

namespace TagAlong.User.API.Controllers;

[ApiController]
[Route("api/users/kyc")]
public class KycController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IKycVerificationRepository _kycRepo;
    private readonly IUserProfileRepository _profiles;
    private readonly INinCacheRepository _ninCache;
    private readonly SmileIdPollService _smilePoll;
    private readonly IConfiguration _config;
    private readonly ILogger<KycController> _logger;

    public KycController(IMediator mediator, IKycVerificationRepository kycRepo,
        IUserProfileRepository profiles, INinCacheRepository ninCache,
        SmileIdPollService smilePoll, IConfiguration config, ILogger<KycController> logger)
    {
        _mediator = mediator;
        _kycRepo = kycRepo;
        _profiles = profiles;
        _ninCache = ninCache;
        _smilePoll = smilePoll;
        _config = config;
        _logger = logger;
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

        // If KYC is still Pending after SmileID has registered the selfie (SmileUserId set),
        // proactively poll get_job_status so the result isn't gated on the final 1210 webhook.
        if (kyc?.Status == KycStatus.Pending
            && !string.IsNullOrEmpty(kyc.SmileJobId)
            && !string.IsNullOrEmpty(kyc.SmileUserId)
            && kyc.JobStartedAt.HasValue
            && (DateTime.UtcNow - kyc.JobStartedAt.Value).TotalSeconds > 20)
        {
            var apiKey    = _config["SmileId:ApiKey"]    ?? string.Empty;
            var partnerId = _config["SmileId:PartnerId"] ?? string.Empty;
            if (!string.IsNullOrEmpty(apiKey))
            {
                var resultJson = await _smilePoll.GetJobStatusResultJsonAsync(
                    kyc.SmileJobId, kyc.SmileUserId, apiKey, partnerId, cancellationToken);

                if (!string.IsNullOrEmpty(resultJson))
                {
                    _logger.LogInformation("KycStatus polling: dispatching job_status result for job={Job}", kyc.SmileJobId);
                    // Pass empty ApiKey so the handler skips signature check (we already authenticated)
                    var cmd = new ProcessSmileWebhookCommand(resultJson, string.Empty, partnerId);
                    await _mediator.Send(cmd, cancellationToken);

                    // Refresh after processing
                    kyc     = await _kycRepo.GetByAuthUserIdAsync(userId.Value, cancellationToken);
                    profile = await _profiles.GetByAuthUserIdAsync(userId.Value, cancellationToken);
                }
            }
        }

        return Ok(new
        {
            isVerified = profile?.IsVerified ?? false,
            verificationStatus = profile?.VerificationStatus.ToString() ?? "None",
            verifiedAt = profile?.VerifiedAt,
            kycStatus = kyc?.Status.ToString(),
            failureReason = kyc?.FailureReason
        });
    }

    /// <summary>
    /// Verify identity using BVN + selfie photo via QoreID.
    /// Performs BVN lookup and facial comparison in one call.
    /// </summary>
    [Authorize]
    [HttpPost("verify-face")]
    public async Task<IActionResult> VerifyFace([FromBody] VerifyFaceRequest request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.BVN) || request.BVN.Trim().Length != 11)
            return BadRequest(new { error = "BVN must be exactly 11 digits" });

        if (string.IsNullOrWhiteSpace(request.PhotoBase64))
            return BadRequest(new { error = "Selfie photo is required" });

        var command = new QoreidFaceVerificationCommand(userId.Value, request.BVN.Trim(), request.PhotoBase64.Trim());
        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
            return StatusCode(500, new { error = result.Error.Message });

        return Ok(result.Value);
    }

    /// <summary>
    /// Called by the app after SmileID SDK completes. Marks the user as verified.
    /// </summary>
    [Authorize]
    [HttpPost("record-smile-job")]
    public async Task<IActionResult> RecordSmileJob([FromBody] RecordSmileJobRequest request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.JobId))
            return BadRequest(new { error = "jobId is required" });

        if (string.IsNullOrWhiteSpace(request.IdNumber) || request.IdNumber.Trim().Length != 11)
            return BadRequest(new { error = "idNumber must be exactly 11 digits" });

        var command = new RecordSmileJobCommand(userId.Value, request.JobId.Trim(), request.IdNumber.Trim());
        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
            return StatusCode(500, new { error = result.Error.Message });

        return Ok(result.Value);
    }

    /// <summary>
    /// Called by the app after Dojah SDK onSuccess fires (legacy — kept for backwards compat).
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

    /// <summary>
    /// Check if a NIN has been previously verified by SmileID and is cached.
    /// Returns 200 with name data if cached, 404 if not.
    /// Flutter uses this to skip the SmileID SDK when the NIN is already known.
    /// </summary>
    [Authorize]
    [HttpGet("nin-lookup/{nin}")]
    public async Task<IActionResult> NinLookup(string nin, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(nin) || nin.Trim().Length != 11)
            return BadRequest(new { error = "NIN must be exactly 11 digits" });

        var entry = await _ninCache.GetByNinAsync(nin.Trim(), cancellationToken);
        if (entry == null)
            return NotFound(new { cached = false });

        return Ok(new
        {
            cached = true,
            firstName = entry.FirstName,
            lastName = entry.LastName,
            middleName = entry.MiddleName,
            dateOfBirth = entry.DateOfBirth,
            gender = entry.Gender
        });
    }

    private Guid? GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }
}

public record VerifyNinRequest(string NIN);
public record VerifyFaceRequest(string BVN, string PhotoBase64);
public record ConfirmSdkRequest(string ReferenceId);
public record RecordSmileJobRequest(string JobId, string IdNumber);
