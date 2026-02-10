using TagAlong.Messaging.Domain.Entities;

namespace TagAlong.Messaging.Domain.Repositories;

public interface IConversationRepository
{
    Task<Conversation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Conversation?> GetByIdWithMessagesAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Conversation?> GetByParticipantsAsync(Guid senderId, Guid travelerId, CancellationToken cancellationToken = default);
    Task<Conversation?> GetByPackageRequestIdAsync(Guid packageRequestId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Conversation>> GetByUserIdAsync(Guid userId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task AddAsync(Conversation conversation, CancellationToken cancellationToken = default);
    void Update(Conversation conversation);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
