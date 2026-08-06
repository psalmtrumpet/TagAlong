using TagAlong.User.Domain.Entities;

namespace TagAlong.User.Domain.Repositories;

public interface IKycVerificationRepository
{
    Task<KycVerification?> GetByAuthUserIdAsync(Guid authUserId, CancellationToken cancellationToken = default);
    Task AddAsync(KycVerification kyc, CancellationToken cancellationToken = default);
    void Update(KycVerification kyc);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
