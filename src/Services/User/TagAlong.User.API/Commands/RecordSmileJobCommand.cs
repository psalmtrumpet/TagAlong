using TagAlong.Common.CQRS;
using TagAlong.Common.Results;
using TagAlong.User.Domain.Entities;
using TagAlong.User.Domain.Repositories;

namespace TagAlong.User.API.Commands;

public record RecordSmileJobCommand(Guid AuthUserId, string JobId, string IdNumber) : ICommand<KycStatusResponse>;

public class RecordSmileJobCommandHandler : ICommandHandler<RecordSmileJobCommand, KycStatusResponse>
{
    private readonly IKycVerificationRepository _kycRepo;
    private readonly IUserProfileRepository _profiles;
    private readonly INinCacheRepository _ninCache;
    private readonly ILogger<RecordSmileJobCommandHandler> _logger;

    public RecordSmileJobCommandHandler(
        IKycVerificationRepository kycRepo,
        IUserProfileRepository profiles,
        INinCacheRepository ninCache,
        ILogger<RecordSmileJobCommandHandler> logger)
    {
        _kycRepo = kycRepo;
        _profiles = profiles;
        _ninCache = ninCache;
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

        // Check NIN cache — if this NIN was previously verified, use cached personal data
        var cached = await _ninCache.GetByNinAsync(request.IdNumber, cancellationToken);

        KycVerification kyc;
        if (existing != null)
        {
            kyc = existing;
            kyc.SetSmileJobId(request.JobId);
            _kycRepo.Update(kyc);
        }
        else
        {
            kyc = KycVerification.Create(request.AuthUserId, smileJobId: request.JobId);
            await _kycRepo.AddAsync(kyc, cancellationToken);
        }

        kyc.Complete(
            nin: request.IdNumber,
            firstName: cached?.FirstName,
            lastName: cached?.LastName,
            middleName: cached?.MiddleName,
            dateOfBirth: cached?.DateOfBirth,
            gender: cached?.Gender,
            nationality: cached != null ? "Nigerian" : null,
            residenceState: null,
            photoPath: null);

        await _kycRepo.SaveChangesAsync(cancellationToken);

        profile.Verify(null);
        _profiles.Update(profile);
        await _profiles.SaveChangesAsync(cancellationToken);

        if (cached != null)
            _logger.LogInformation("SmileID job {JobId} verified user {UserId} using NIN cache", request.JobId, request.AuthUserId);
        else
            _logger.LogInformation("SmileID job {JobId} verified user {UserId} (webhook will populate name data)", request.JobId, request.AuthUserId);

        return Result.Success(new KycStatusResponse(true, "Verified", "Identity verified successfully"));
    }
}
