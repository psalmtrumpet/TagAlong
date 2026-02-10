using Microsoft.EntityFrameworkCore;
using TagAlong.Messaging.Domain.Entities;
using TagAlong.Messaging.Domain.Repositories;
using TagAlong.Messaging.Infrastructure.Persistence;

namespace TagAlong.Messaging.Infrastructure.Repositories;

public class MessageRepository : IMessageRepository
{
    private readonly MessagingDbContext _context;

    public MessageRepository(MessagingDbContext context)
    {
        _context = context;
    }

    public async Task<Message?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Messages.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
    }

    public async Task<IEnumerable<Message>> GetByConversationIdAsync(Guid conversationId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        return await _context.Messages
            .Where(m => m.ConversationId == conversationId)
            .OrderByDescending(m => m.SentAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetUnreadCountAsync(Guid conversationId, Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.Messages
            .Where(m => m.ConversationId == conversationId && m.SenderId != userId && m.ReadAt == null)
            .CountAsync(cancellationToken);
    }

    public async Task<Message?> GetLatestPriceProposalAsync(Guid conversationId, CancellationToken cancellationToken = default)
    {
        return await _context.Messages
            .Where(m => m.ConversationId == conversationId &&
                       (m.MessageType == MessageType.PriceProposal || m.MessageType == MessageType.PriceAccepted))
            .OrderByDescending(m => m.SentAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task AddAsync(Message message, CancellationToken cancellationToken = default)
    {
        await _context.Messages.AddAsync(message, cancellationToken);
    }

    public void Update(Message message)
    {
        _context.Messages.Update(message);
    }

    public async Task MarkAllAsReadAsync(Guid conversationId, Guid userId, CancellationToken cancellationToken = default)
    {
        await _context.Messages
            .Where(m => m.ConversationId == conversationId && m.SenderId != userId && m.ReadAt == null)
            .ExecuteUpdateAsync(m => m.SetProperty(x => x.ReadAt, DateTime.UtcNow), cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
