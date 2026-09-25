using MediatR;
using TagAlong.Common.CQRS;
using TagAlong.Common.Results;
using TagAlong.User.API.Services;
using TagAlong.User.Domain.Entities;
using TagAlong.User.Domain.Repositories;

namespace TagAlong.User.API.Commands;

public record RecordSmileJobCommand(Guid AuthUserId, string JobId, string IdNumber, string? SmileUserId = null) : ICommand<KycStatusResponse>;

public class RecordSmileJobCommandHandler : ICommandHandler<RecordSmileJobCommand, KycStatusResponse>
{
    private readonly IKycVerificationRepository _kycRepo;
    private readonly IUserProfileRepository _profiles;
    private readonly INinCacheRepository _ninCache;
    private readonly IEmailService _email;
    private readonly IMediator _mediator;
    private readonly SmileIdPollService _smilePoll;
    private readonly IConfiguration _config;
    private readonly ILogger<RecordSmileJobCommandHandler> _logger;

    public RecordSmileJobCommandHandler(
        IKycVerificationRepository kycRepo,
        IUserProfileRepository profiles,
        INinCacheRepository ninCache,
        IEmailService email,
        IMediator mediator,
        SmileIdPollService smilePoll,
        IConfiguration config,
        ILogger<RecordSmileJobCommandHandler> logger)
    {
        _kycRepo = kycRepo;
        _profiles = profiles;
        _ninCache = ninCache;
        _email = email;
        _mediator = mediator;
        _smilePoll = smilePoll;
        _config = config;
        _logger = logger;
    }

    public async Task<Result<KycStatusResponse>> Handle(RecordSmileJobCommand request, CancellationToken cancellationToken)
    {
        var profile = await _profiles.GetByAuthUserIdAsync(request.AuthUserId, cancellationToken);
        if (profile == null)
            return Result.Failure<KycStatusResponse>(Error.NotFound("User profile not found"));

        if (profile.IsVerified)
            return Result.Success(new KycStatusResponse(true, "Verified", "Already verified"));

        var existing = await _kycRepo.GetByAuthUserIdAsync(request.AuthUserId, cancellationToken);
        _logger.LogInformation("record-smile-job: userId={UserId} newJobId={JobId} existing={ExistingJobId} existingStatus={Status}",
            request.AuthUserId, request.JobId, existing?.SmileJobId ?? "none", existing?.Status.ToString() ?? "none");

        KycVerification kyc;
        if (existing != null)
        {
            kyc = existing;
            // Reset so a previously failed attempt (e.g. QoreID, old flow) doesn't bleed through
            kyc.ResetForRetry(request.JobId);
            // Store SmileUserId on existing entity before Update call
            if (!string.IsNullOrEmpty(request.SmileUserId) && string.IsNullOrEmpty(kyc.SmileUserId))
                kyc.SetSmileUserId(request.SmileUserId);
            _kycRepo.Update(kyc);
        }
        else
        {
            kyc = KycVerification.Create(request.AuthUserId, smileJobId: request.JobId);
            // Set SmileUserId before AddAsync so it's included in the INSERT, not a separate UPDATE
            if (!string.IsNullOrEmpty(request.SmileUserId))
                kyc.SetSmileUserId(request.SmileUserId);
            await _kycRepo.AddAsync(kyc, cancellationToken);
        }

        var cached = await _ninCache.GetByNinAsync(request.IdNumber, cancellationToken);

        if (cached != null && (cached.FirstName != null || cached.LastName != null))
        {
            // NIN already verified by SmileID — trust the cached result directly
            kyc.Complete(
                nin: request.IdNumber,
                firstName: cached.FirstName,
                lastName: cached.LastName,
                middleName: cached.MiddleName,
                dateOfBirth: cached.DateOfBirth,
                gender: cached.Gender,
                nationality: "Nigerian",
                residenceState: null,
                photoPath: null);

            await _kycRepo.SaveChangesAsync(cancellationToken);
            profile.Verify(null);
            _profiles.Update(profile);
            await _profiles.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Job {JobId} verified user {UserId} via NIN cache (name matched)", request.JobId, request.AuthUserId);
            return Result.Success(new KycStatusResponse(true, "Verified", "Identity verified successfully"));
        }

        // No cached name data — store jobId as Pending; webhook will do the name check and complete
        profile.MarkVerificationPending();
        _profiles.Update(profile);
        await _kycRepo.SaveChangesAsync(cancellationToken);
        await _profiles.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Job {JobId} stored as Pending for user {UserId} — awaiting SmileID webhook", request.JobId, request.AuthUserId);

        // Immediately poll get_job_status — catches results that arrived before this record was created (race condition)
        if (!string.IsNullOrEmpty(request.SmileUserId))
        {
            var apiKey    = _config["SmileId:ApiKey"]    ?? string.Empty;
            var partnerId = _config["SmileId:PartnerId"] ?? string.Empty;
            if (!string.IsNullOrEmpty(apiKey))
            {
                try
                {
                    var resultJson = await _smilePoll.GetJobStatusResultJsonAsync(
                        request.JobId, request.SmileUserId, apiKey, partnerId, cancellationToken);
                    if (!string.IsNullOrEmpty(resultJson))
                    {
                        _logger.LogInformation("record-smile-job: immediate poll found result for job {JobId}", request.JobId);
                        var cmd = new ProcessSmileWebhookCommand(resultJson, string.Empty, partnerId, IsJobStatusResult: true);
                        var pollResult = await _mediator.Send(cmd, cancellationToken);
                        if (pollResult.IsSuccess && pollResult.Value.Processed)
                        {
                            // Re-read to get the updated status after processing
                            var updatedProfile = await _profiles.GetByAuthUserIdAsync(request.AuthUserId, cancellationToken);
                            if (updatedProfile?.IsVerified == true)
                                return Result.Success(new KycStatusResponse(true, "Verified", "Identity verified successfully"));
                            var updatedKyc = await _kycRepo.GetByAuthUserIdAsync(request.AuthUserId, cancellationToken);
                            if (updatedKyc?.Status == KycStatus.Failed)
                                return Result.Success(new KycStatusResponse(false, "Failed", updatedKyc.FailureReason ?? "Verification failed"));
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "record-smile-job: immediate poll failed for job {JobId}", request.JobId);
                }
            }
        }

        return Result.Success(new KycStatusResponse(false, "Pending", "Verifying your identity. This usually takes a few seconds."));
    }
}
