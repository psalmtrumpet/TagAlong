using TagAlong.User.Domain.Entities;

namespace TagAlong.User.Domain.Repositories;

public interface INinCacheRepository
{
    Task<NinCache?> GetByNinAsync(string nin, CancellationToken cancellationToken = default);
    /// <summary>The key a NIN is stored under (a keyed hash — the NIN itself is never stored).</summary>
    string KeyFor(string nin);
    Task AddAsync(NinCache entry, CancellationToken cancellationToken = default);
    void Update(NinCache entry);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
