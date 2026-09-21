using TagAlong.User.Domain.Entities;

namespace TagAlong.User.Domain.Repositories;

public interface ISmileWebhookLogRepository
{
    Task AddAsync(SmileWebhookLog log, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
