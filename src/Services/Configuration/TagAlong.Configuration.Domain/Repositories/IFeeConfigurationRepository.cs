namespace TagAlong.Configuration.Domain.Repositories;

public interface IFeeConfigurationRepository
{
    Task<Entities.FeeConfiguration?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Entities.FeeConfiguration?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<Entities.FeeConfiguration?> GetActiveAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<Entities.FeeConfiguration>> GetAllAsync(int page, int pageSize, CancellationToken cancellationToken = default);
    Task AddAsync(Entities.FeeConfiguration feeConfiguration, CancellationToken cancellationToken = default);
    void Update(Entities.FeeConfiguration feeConfiguration);
    void Delete(Entities.FeeConfiguration feeConfiguration);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
