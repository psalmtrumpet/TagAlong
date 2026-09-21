using TagAlong.User.Domain.Entities;

namespace TagAlong.User.Domain.Repositories;

public interface INinCacheRepository
{
    Task<NinCache?> GetByNinAsync(string nin, CancellationToken cancellationToken = default);
    Task AddAsync(NinCache entry, CancellationToken cancellationToken = default);
    void Update(NinCache entry);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
