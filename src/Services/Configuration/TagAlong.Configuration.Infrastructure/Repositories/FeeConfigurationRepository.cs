using Microsoft.EntityFrameworkCore;
using TagAlong.Configuration.Domain.Entities;
using TagAlong.Configuration.Domain.Repositories;
using TagAlong.Configuration.Infrastructure.Persistence;

namespace TagAlong.Configuration.Infrastructure.Repositories;

public class FeeConfigurationRepository : IFeeConfigurationRepository
{
    private readonly ConfigurationDbContext _context;

    public FeeConfigurationRepository(ConfigurationDbContext context)
    {
        _context = context;
    }

    public async Task<FeeConfiguration?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.FeeConfigurations.FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
    }

    public async Task<FeeConfiguration?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return await _context.FeeConfigurations.FirstOrDefaultAsync(f => f.Name == name, cancellationToken);
    }

    public async Task<FeeConfiguration?> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        return await _context.FeeConfigurations.FirstOrDefaultAsync(f => f.IsActive, cancellationToken);
    }

    public async Task<IEnumerable<FeeConfiguration>> GetAllAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        return await _context.FeeConfigurations
            .OrderByDescending(f => f.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(FeeConfiguration feeConfiguration, CancellationToken cancellationToken = default)
    {
        await _context.FeeConfigurations.AddAsync(feeConfiguration, cancellationToken);
    }

    public void Update(FeeConfiguration feeConfiguration)
    {
        _context.FeeConfigurations.Update(feeConfiguration);
    }

    public void Delete(FeeConfiguration feeConfiguration)
    {
        _context.FeeConfigurations.Remove(feeConfiguration);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
