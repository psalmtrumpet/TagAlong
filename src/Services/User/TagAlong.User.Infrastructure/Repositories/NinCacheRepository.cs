using Microsoft.EntityFrameworkCore;
using TagAlong.User.Domain.Entities;
using TagAlong.User.Domain.Repositories;
using TagAlong.User.Infrastructure.Persistence;

namespace TagAlong.User.Infrastructure.Repositories;

public class NinCacheRepository : INinCacheRepository
{
    private readonly UserDbContext _context;

    public NinCacheRepository(UserDbContext context)
    {
        _context = context;
    }

    public async Task<NinCache?> GetByNinAsync(string nin, CancellationToken cancellationToken = default)
        => await _context.NinCaches.FirstOrDefaultAsync(n => n.NIN == nin, cancellationToken);

    public async Task AddAsync(NinCache entry, CancellationToken cancellationToken = default)
        => await _context.NinCaches.AddAsync(entry, cancellationToken);

    public void Update(NinCache entry)
        => _context.NinCaches.Update(entry);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => await _context.SaveChangesAsync(cancellationToken);
}
