using Microsoft.EntityFrameworkCore;
using TagAlong.Configuration.Domain.Entities;
using TagAlong.Configuration.Domain.Repositories;
using TagAlong.Configuration.Infrastructure.Persistence;

namespace TagAlong.Configuration.Infrastructure.Repositories;

public class PlatformConfigurationRepository : IPlatformConfigurationRepository
{
    private readonly ConfigurationDbContext _context;

    public PlatformConfigurationRepository(ConfigurationDbContext context)
    {
        _context = context;
    }

    public async Task<PlatformConfiguration?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.PlatformConfigurations.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<PlatformConfiguration?> GetByKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        return await _context.PlatformConfigurations.FirstOrDefaultAsync(c => c.Key == key, cancellationToken);
    }

    public async Task<IEnumerable<PlatformConfiguration>> GetByTypeAsync(ConfigurationType type, CancellationToken cancellationToken = default)
    {
        return await _context.PlatformConfigurations
            .Where(c => c.Type == type)
            .OrderBy(c => c.Key)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<PlatformConfiguration>> GetAllActiveAsync(CancellationToken cancellationToken = default)
    {
        return await _context.PlatformConfigurations
            .Where(c => c.IsActive)
            .OrderBy(c => c.Key)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<PlatformConfiguration>> GetAllAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        return await _context.PlatformConfigurations
            .OrderBy(c => c.Key)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(PlatformConfiguration configuration, CancellationToken cancellationToken = default)
    {
        await _context.PlatformConfigurations.AddAsync(configuration, cancellationToken);
    }

    public void Update(PlatformConfiguration configuration)
    {
        _context.PlatformConfigurations.Update(configuration);
    }

    public void Delete(PlatformConfiguration configuration)
    {
        _context.PlatformConfigurations.Remove(configuration);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
