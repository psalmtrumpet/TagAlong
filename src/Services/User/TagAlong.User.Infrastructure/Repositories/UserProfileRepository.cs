using Microsoft.EntityFrameworkCore;
using TagAlong.User.Domain.Entities;
using TagAlong.User.Domain.Repositories;
using TagAlong.User.Infrastructure.Persistence;

namespace TagAlong.User.Infrastructure.Repositories;

public class UserProfileRepository : IUserProfileRepository
{
    private readonly UserDbContext _context;

    public UserProfileRepository(UserDbContext context)
    {
        _context = context;
    }

    public async Task<UserProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.UserProfiles.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    public async Task<UserProfile?> GetByAuthUserIdAsync(Guid authUserId, CancellationToken cancellationToken = default)
    {
        return await _context.UserProfiles.FirstOrDefaultAsync(u => u.AuthUserId == authUserId, cancellationToken);
    }

    public async Task<UserProfile?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await _context.UserProfiles.FirstOrDefaultAsync(u => u.Email == email.ToLowerInvariant(), cancellationToken);
    }

    public async Task<IEnumerable<UserProfile>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        return await _context.UserProfiles.Where(u => ids.Contains(u.Id)).ToListAsync(cancellationToken);
    }

    public async Task AddAsync(UserProfile profile, CancellationToken cancellationToken = default)
    {
        await _context.UserProfiles.AddAsync(profile, cancellationToken);
    }

    public void Update(UserProfile profile)
    {
        _context.UserProfiles.Update(profile);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IEnumerable<UserProfile>> SearchAvailableUsersAsync(
        double latitude,
        double longitude,
        double radiusKm,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        // Bounding box pre-filter (follows TripRepository pattern)
        var minLat = latitude - (radiusKm / 111.0);
        var maxLat = latitude + (radiusKm / 111.0);
        var minLon = longitude - (radiusKm / (111.0 * Math.Cos(latitude * Math.PI / 180)));
        var maxLon = longitude + (radiusKm / (111.0 * Math.Cos(latitude * Math.PI / 180)));

        var candidates = await _context.UserProfiles
            .Where(u => u.IsAvailable)
            .Where(u => u.CurrentLatitude.HasValue && u.CurrentLongitude.HasValue)
            .Where(u => u.CurrentLatitude >= minLat && u.CurrentLatitude <= maxLat)
            .Where(u => u.CurrentLongitude >= minLon && u.CurrentLongitude <= maxLon)
            .Where(u => !u.AvailabilityExpiresAt.HasValue || u.AvailabilityExpiresAt > DateTime.UtcNow)
            .ToListAsync(cancellationToken);

        // Precise Haversine filter and sort by distance
        return candidates
            .Where(u => u.DistanceFromKm(latitude, longitude) <= radiusKm)
            .OrderBy(u => u.DistanceFromKm(latitude, longitude))
            .Skip((page - 1) * pageSize)
            .Take(pageSize);
    }

    public async Task<int> GetAvailableUsersCountAsync(
        double latitude,
        double longitude,
        double radiusKm,
        CancellationToken cancellationToken = default)
    {
        // Bounding box pre-filter
        var minLat = latitude - (radiusKm / 111.0);
        var maxLat = latitude + (radiusKm / 111.0);
        var minLon = longitude - (radiusKm / (111.0 * Math.Cos(latitude * Math.PI / 180)));
        var maxLon = longitude + (radiusKm / (111.0 * Math.Cos(latitude * Math.PI / 180)));

        var candidates = await _context.UserProfiles
            .Where(u => u.IsAvailable)
            .Where(u => u.CurrentLatitude.HasValue && u.CurrentLongitude.HasValue)
            .Where(u => u.CurrentLatitude >= minLat && u.CurrentLatitude <= maxLat)
            .Where(u => u.CurrentLongitude >= minLon && u.CurrentLongitude <= maxLon)
            .Where(u => !u.AvailabilityExpiresAt.HasValue || u.AvailabilityExpiresAt > DateTime.UtcNow)
            .ToListAsync(cancellationToken);

        // Precise Haversine filter
        return candidates.Count(u => u.DistanceFromKm(latitude, longitude) <= radiusKm);
    }

    public async Task ExpireStaleAvailabilityAsync(CancellationToken cancellationToken = default)
    {
        var expiredUsers = await _context.UserProfiles
            .Where(u => u.IsAvailable)
            .Where(u => u.AvailabilityExpiresAt.HasValue && u.AvailabilityExpiresAt <= DateTime.UtcNow)
            .ToListAsync(cancellationToken);

        foreach (var user in expiredUsers)
        {
            user.SetUnavailable();
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
