using Microsoft.EntityFrameworkCore;
using TagAlong.Messaging.Domain.Entities;
using TagAlong.Messaging.Domain.Repositories;
using TagAlong.Messaging.Infrastructure.Persistence;

namespace TagAlong.Messaging.Infrastructure.Repositories;

public class ConversationRepository : IConversationRepository
{
    private readonly MessagingDbContext _context;

    public ConversationRepository(MessagingDbContext context)
    {
        _context = context;
    }

    public async Task<Conversation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Conversations.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<Conversation?> GetByIdWithMessagesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Conversations
            .Include(c => c.Messages.OrderByDescending(m => m.SentAt).Take(50))
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    // An open (not Declined/Closed) conversation between the pair for the same trip.
    // Requests without a trip (e.g. from Nearby) match other trip-less conversations.
    public async Task<Conversation?> GetOpenByParticipantsAsync(Guid senderId, Guid travelerId, Guid? tripId, CancellationToken cancellationToken = default)
    {
        return await _context.Conversations
            .Where(c =>
                ((c.SenderId == senderId && c.TravelerId == travelerId) ||
                 (c.SenderId == travelerId && c.TravelerId == senderId)) &&
                c.TripId == tripId &&
                c.Status != ConversationStatus.Declined &&
                c.Status != ConversationStatus.Closed)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    // Bookings currently holding a seat / package slot on a trip.
    public async Task<int> CountActiveBookingsByTripIdAsync(Guid tripId, CancellationToken cancellationToken = default)
    {
        return await _context.Conversations
            .CountAsync(c => c.TripId == tripId &&
                (c.Status == ConversationStatus.LockedIn || c.Status == ConversationStatus.InProgress), cancellationToken);
    }

    public async Task<Conversation?> GetByPackageRequestIdAsync(Guid packageRequestId, CancellationToken cancellationToken = default)
    {
        return await _context.Conversations
            .FirstOrDefaultAsync(c => c.PackageRequestId == packageRequestId, cancellationToken);
    }

    public async Task<IEnumerable<Conversation>> GetByUserIdAsync(Guid userId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        return await _context.Conversations
            .Where(c => (c.SenderId == userId || c.TravelerId == userId)
                     && c.Status != ConversationStatus.Closed
                     && c.Status != ConversationStatus.Declined)
            .OrderByDescending(c => c.UpdatedAt ?? c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    // Bookings on a trip that haven't started yet (an in-progress ride is never cut off).
    public async Task<IEnumerable<Conversation>> GetUnstartedByTripIdAsync(Guid tripId, CancellationToken cancellationToken = default)
    {
        return await _context.Conversations
            .Where(c => c.TripId == tripId &&
                        (c.Status == ConversationStatus.Pending ||
                         c.Status == ConversationStatus.Negotiating ||
                         c.Status == ConversationStatus.Active ||
                         c.Status == ConversationStatus.LockedIn))
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountInProgressByTravelerIdAsync(Guid travelerId, CancellationToken cancellationToken = default)
    {
        return await _context.Conversations
            .CountAsync(c => c.TravelerId == travelerId && c.Status == ConversationStatus.InProgress, cancellationToken);
    }

    public async Task AddAsync(Conversation conversation, CancellationToken cancellationToken = default)
    {
        await _context.Conversations.AddAsync(conversation, cancellationToken);
    }

    public void Update(Conversation conversation)
    {
        _context.Conversations.Update(conversation);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
