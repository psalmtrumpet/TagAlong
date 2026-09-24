using TagAlong.Identity.Domain.Entities;

namespace TagAlong.Identity.Domain.Repositories;

public interface IWaitlistRepository
{
    Task<bool> ExistsAsync(string email, CancellationToken ct = default);
    Task AddAsync(WaitlistEntry entry, CancellationToken ct = default);
    Task<IReadOnlyList<WaitlistEntry>> GetAllAsync(CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
