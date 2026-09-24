using Microsoft.EntityFrameworkCore;
using TagAlong.Identity.Domain.Entities;
using TagAlong.Identity.Domain.Repositories;
using TagAlong.Identity.Infrastructure.Persistence;

namespace TagAlong.Identity.Infrastructure.Repositories;

public class WaitlistRepository : IWaitlistRepository
{
    private readonly IdentityDbContext _db;

    public WaitlistRepository(IdentityDbContext db) => _db = db;

    public Task<bool> ExistsAsync(string email, CancellationToken ct = default)
        => _db.WaitlistEntries.AnyAsync(w => w.Email == email.ToLowerInvariant(), ct);

    public async Task AddAsync(WaitlistEntry entry, CancellationToken ct = default)
        => await _db.WaitlistEntries.AddAsync(entry, ct);

    public async Task<IReadOnlyList<WaitlistEntry>> GetAllAsync(CancellationToken ct = default)
        => await _db.WaitlistEntries.OrderByDescending(w => w.JoinedAt).ToListAsync(ct);

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);
}
