using TagAlong.Identity.Domain.Entities;

namespace TagAlong.Identity.Domain.Repositories;

public interface IVerificationCodeRepository
{
    Task<VerificationCode?> GetValidCodeAsync(Guid userId, string code, VerificationType type, CancellationToken cancellationToken = default);
    Task AddAsync(VerificationCode verificationCode, CancellationToken cancellationToken = default);
    Task InvalidatePreviousCodesAsync(Guid userId, VerificationType type, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
