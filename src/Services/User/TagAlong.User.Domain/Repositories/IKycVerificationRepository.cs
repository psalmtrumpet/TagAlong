using TagAlong.User.Domain.Entities;

namespace TagAlong.User.Domain.Repositories;

public interface IKycVerificationRepository
{
    Task<KycVerification?> GetByAuthUserIdAsync(Guid authUserId, CancellationToken cancellationToken = default);
    Task<KycVerification?> GetByQoreIdReferenceAsync(string reference, CancellationToken cancellationToken = default);
    Task<KycVerification?> GetBySmileJobIdAsync(string jobId, CancellationToken cancellationToken = default);
    Task AddAsync(KycVerification kyc, CancellationToken cancellationToken = default);
    /// <summary>
    /// True when another account is already verified with this NIN. Older records only kept the
    /// masked NIN, so those match on the masked number plus date of birth.
    /// </summary>
    Task<bool> NinUsedByAnotherAccountAsync(Guid authUserId, string? ninHash, string? maskedNin, string? dateOfBirth, CancellationToken cancellationToken = default);
    void Update(KycVerification kyc);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
