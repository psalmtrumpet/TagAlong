using TagAlong.Common.CQRS;
using TagAlong.Common.Results;
using TagAlong.User.Domain.Entities;
using TagAlong.User.Domain.Repositories;

namespace TagAlong.User.API.Commands;

public record RecordSmileJobCommand(Guid AuthUserId, string JobId, string NIN) : ICommand<KycStatusResponse>;

public class RecordSmileJobCommandHandler : ICommandHandler<RecordSmileJobCommand, KycStatusResponse>
{
    private readonly IKycVerificationRepository _kycRepo;
    private readonly ILogger<RecordSmileJobCommandHandler> _logger;

    public RecordSmileJobCommandHandler(IKycVerificationRepository kycRepo, ILogger<RecordSmileJobCommandHandler> logger)
    {
        _kycRepo = kycRepo;
        _logger = logger;
    }

    public async Task<Result<KycStatusResponse>> Handle(RecordSmileJobCommand request, CancellationToken cancellationToken)
    {
        var existing = await _kycRepo.GetByAuthUserIdAsync(request.AuthUserId, cancellationToken);

        if (existing != null && existing.Status == KycStatus.Completed)
            return Result.Success(new KycStatusResponse(true, "Verified", "Already verified"));

        if (existing != null && existing.Status == KycStatus.Pending)
        {
            existing.SetSmileJobId(request.JobId);
            _kycRepo.Update(existing);
        }
        else
        {
            var kyc = KycVerification.Create(request.AuthUserId, smileJobId: request.JobId);
            await _kycRepo.AddAsync(kyc, cancellationToken);
        }

        await _kycRepo.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Smile ID job {JobId} recorded for user {UserId}", request.JobId, request.AuthUserId);

        return Result.Success(new KycStatusResponse(false, "Pending", "Verification submitted. We'll update your status shortly."));
    }
}
