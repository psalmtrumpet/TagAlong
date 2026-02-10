using TagAlong.User.Domain.Entities;

namespace TagAlong.User.Domain.Repositories;

public interface IUserProfileRepository
{
    Task<UserProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<UserProfile?> GetByAuthUserIdAsync(Guid authUserId, CancellationToken cancellationToken = default);
    Task<UserProfile?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<IEnumerable<UserProfile>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);
    Task AddAsync(UserProfile profile, CancellationToken cancellationToken = default);
    void Update(UserProfile profile);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    // Availability search methods
    Task<IEnumerable<UserProfile>> SearchAvailableUsersAsync(
        double latitude,
        double longitude,
        double radiusKm,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<int> GetAvailableUsersCountAsync(
        double latitude,
        double longitude,
        double radiusKm,
        CancellationToken cancellationToken = default);

    Task ExpireStaleAvailabilityAsync(CancellationToken cancellationToken = default);
}
