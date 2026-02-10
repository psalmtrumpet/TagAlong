using Microsoft.EntityFrameworkCore;
using TagAlong.Identity.Domain.Entities;
using TagAlong.Identity.Domain.Repositories;
using TagAlong.Identity.Infrastructure.Persistence;

namespace TagAlong.Identity.Infrastructure.Repositories;

public class VerificationCodeRepository : IVerificationCodeRepository
{
    private readonly IdentityDbContext _context;

    public VerificationCodeRepository(IdentityDbContext context)
    {
        _context = context;
    }

    public async Task<VerificationCode?> GetValidCodeAsync(
        Guid userId,
        string code,
        VerificationType type,
        CancellationToken cancellationToken = default)
    {
        return await _context.VerificationCodes
            .FirstOrDefaultAsync(vc =>
                vc.UserId == userId &&
                vc.Code == code &&
                vc.Type == type &&
                !vc.IsUsed &&
                vc.ExpiresAt > DateTime.UtcNow,
                cancellationToken);
    }

    public async Task AddAsync(VerificationCode verificationCode, CancellationToken cancellationToken = default)
    {
        await _context.VerificationCodes.AddAsync(verificationCode, cancellationToken);
    }

    public async Task InvalidatePreviousCodesAsync(
        Guid userId,
        VerificationType type,
        CancellationToken cancellationToken = default)
    {
        var previousCodes = await _context.VerificationCodes
            .Where(vc => vc.UserId == userId && vc.Type == type && !vc.IsUsed)
            .ToListAsync(cancellationToken);

        foreach (var code in previousCodes)
        {
            code.MarkAsUsed();
        }
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
