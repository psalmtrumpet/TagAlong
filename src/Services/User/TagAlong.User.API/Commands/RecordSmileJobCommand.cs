using TagAlong.Common.CQRS;
using TagAlong.Common.Results;
using TagAlong.User.API.Services;
using TagAlong.User.Domain.Entities;
using TagAlong.User.Domain.Repositories;

namespace TagAlong.User.API.Commands;

public record RecordSmileJobCommand(Guid AuthUserId, string JobId, string IdNumber) : ICommand<KycStatusResponse>;

public class RecordSmileJobCommandHandler : ICommandHandler<RecordSmileJobCommand, KycStatusResponse>
{
    private readonly IKycVerificationRepository _kycRepo;
    private readonly IUserProfileRepository _profiles;
    private readonly INinCacheRepository _ninCache;
    private readonly IEmailService _email;
    private readonly ILogger<RecordSmileJobCommandHandler> _logger;

    public RecordSmileJobCommandHandler(
        IKycVerificationRepository kycRepo,
        IUserProfileRepository profiles,
        INinCacheRepository ninCache,
        IEmailService email,
        ILogger<RecordSmileJobCommandHandler> logger)
    {
        _kycRepo = kycRepo;
        _profiles = profiles;
        _ninCache = ninCache;
        _email = email;
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
        if (existing != null && existing.Status == KycStatus.Completed)
            return Result.Success(new KycStatusResponse(true, "Verified", "Already verified"));

        KycVerification kyc;
        if (existing != null)
        {
            kyc = existing;
            // Reset so a previously failed attempt (e.g. QoreID, old flow) doesn't bleed through
            kyc.ResetForRetry(request.JobId);
            _kycRepo.Update(kyc);
        }
        else
        {
            kyc = KycVerification.Create(request.AuthUserId, smileJobId: request.JobId);
            await _kycRepo.AddAsync(kyc, cancellationToken);
        }

        var cached = await _ninCache.GetByNinAsync(request.IdNumber, cancellationToken);

        if (cached != null && (cached.FirstName != null || cached.LastName != null))
        {
            // Name data available — verify immediately without waiting for webhook
            if (!NinNameMatcher.NamesMatch(profile.FirstName, profile.LastName, cached.FirstName, cached.LastName))
            {
                var reason = NinNameMatcher.BuildMismatchReason(
                    profile.FirstName, profile.LastName, cached.FirstName, cached.LastName);
                kyc.Fail(reason);
                _kycRepo.Update(kyc);
                await _kycRepo.SaveChangesAsync(cancellationToken);

                await _email.SendAsync(
                    profile.Email,
                    $"{profile.FirstName} {profile.LastName}",
                    "TagAlong — Identity Verification Failed",
                    NinNameMatcher.BuildFailureEmailHtml(profile.FirstName, reason),
                    cancellationToken);

                _logger.LogWarning("NIN cache name mismatch for user {UserId}", request.AuthUserId);
                return Result.Success(new KycStatusResponse(false, "NameMismatch", reason));
            }

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
        return Result.Success(new KycStatusResponse(false, "Pending", "Verifying your identity. This usually takes a few seconds."));
    }
}
