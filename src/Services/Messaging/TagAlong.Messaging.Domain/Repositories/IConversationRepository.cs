using TagAlong.Messaging.Domain.Entities;

namespace TagAlong.Messaging.Domain.Repositories;

public interface IConversationRepository
{
    Task<Conversation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Conversation?> GetByIdWithMessagesAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Conversation?> GetOpenByParticipantsAsync(Guid senderId, Guid travelerId, Guid? tripId, CancellationToken cancellationToken = default);
    Task<int> CountActiveBookingsByTripIdAsync(Guid tripId, CancellationToken cancellationToken = default);
    Task<Conversation?> GetByPackageRequestIdAsync(Guid packageRequestId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Conversation>> GetByUserIdAsync(Guid userId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<IEnumerable<Conversation>> GetUnstartedByTripIdAsync(Guid tripId, CancellationToken cancellationToken = default);
    /// <summary>Passengers currently on board this trip.</summary>
    Task<IEnumerable<Conversation>> GetInProgressByTripIdAsync(Guid tripId, CancellationToken cancellationToken = default);
    /// <summary>On-board rides on a trip that haven't changed since <paramref name="before"/>.</summary>
    Task<IEnumerable<Conversation>> GetInProgressRidesUnchangedSinceAsync(DateTime before, CancellationToken cancellationToken = default);
    Task<int> CountInProgressByTravelerIdAsync(Guid travelerId, CancellationToken cancellationToken = default);
    /// <summary>Rides still under way whose car reached the drop-off before this time.</summary>
    Task<IEnumerable<Conversation>> GetRidesAtDropSinceAsync(DateTime before, CancellationToken cancellationToken = default);
    Task AddAsync(Conversation conversation, CancellationToken cancellationToken = default);
    void Update(Conversation conversation);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
