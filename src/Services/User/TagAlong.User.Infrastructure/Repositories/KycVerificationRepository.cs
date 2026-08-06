using Microsoft.EntityFrameworkCore;
using TagAlong.User.Domain.Entities;
using TagAlong.User.Domain.Repositories;
using TagAlong.User.Infrastructure.Persistence;

namespace TagAlong.User.Infrastructure.Repositories;

public class KycVerificationRepository : IKycVerificationRepository
{
    private readonly UserDbContext _context;

    public KycVerificationRepository(UserDbContext context)
    {
        _context = context;
    }

    public async Task<KycVerification?> GetByAuthUserIdAsync(Guid authUserId, CancellationToken cancellationToken = default)
        => await _context.KycVerifications
            .Where(k => k.AuthUserId == authUserId)
            .OrderByDescending(k => k.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task AddAsync(KycVerification kyc, CancellationToken cancellationToken = default)
        => await _context.KycVerifications.AddAsync(kyc, cancellationToken);

    public void Update(KycVerification kyc)
        => _context.KycVerifications.Update(kyc);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => await _context.SaveChangesAsync(cancellationToken);
}
