using TagAlong.User.Domain.Entities;
using TagAlong.User.Domain.Repositories;
using TagAlong.User.Infrastructure.Persistence;

namespace TagAlong.User.Infrastructure.Repositories;

public class SmileWebhookLogRepository : ISmileWebhookLogRepository
{
    private readonly UserDbContext _context;

    public SmileWebhookLogRepository(UserDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(SmileWebhookLog log, CancellationToken cancellationToken = default)
        => await _context.SmileWebhookLogs.AddAsync(log, cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => await _context.SaveChangesAsync(cancellationToken);
}
