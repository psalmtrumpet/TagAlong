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
    private readonly ILogger<RecordSmileJobCommandHandler> _logger;

    public RecordSmileJobCommandHandler(
        IKycVerificationRepository kycRepo,
        IUserProfileRepository profiles,
        ILogger<RecordSmileJobCommandHandler> logger)
    {
        _kycRepo = kycRepo;
        _profiles = profiles;
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

        KycVerification kyc;
        if (existing != null && existing.Status == KycStatus.Completed)
            return Result.Success(new KycStatusResponse(true, "Verified", "Already verified"));

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
            firstName: null,
            lastName: null,
            middleName: null,
            dateOfBirth: null,
            gender: null,
            nationality: null,
            residenceState: null,
            photoPath: null);

        await _kycRepo.SaveChangesAsync(cancellationToken);

        profile.Verify(null);
        _profiles.Update(profile);
        await _profiles.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("SmileID job {JobId} verified user {UserId}", request.JobId, request.AuthUserId);

        return Result.Success(new KycStatusResponse(true, "Verified", "Identity verified successfully"));
    }
}
