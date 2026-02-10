namespace TagAlong.Configuration.Domain.Repositories;

public interface IPlatformConfigurationRepository
{
    Task<Entities.PlatformConfiguration?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Entities.PlatformConfiguration?> GetByKeyAsync(string key, CancellationToken cancellationToken = default);
    Task<IEnumerable<Entities.PlatformConfiguration>> GetByTypeAsync(Entities.ConfigurationType type, CancellationToken cancellationToken = default);
    Task<IEnumerable<Entities.PlatformConfiguration>> GetAllActiveAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<Entities.PlatformConfiguration>> GetAllAsync(int page, int pageSize, CancellationToken cancellationToken = default);
    Task AddAsync(Entities.PlatformConfiguration configuration, CancellationToken cancellationToken = default);
    void Update(Entities.PlatformConfiguration configuration);
    void Delete(Entities.PlatformConfiguration configuration);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
