using TripEntity = TagAlong.Trip.Domain.Entities.Trip;

namespace TagAlong.Trip.Infrastructure.Services;

/// <summary>Pairing of a trip with its verified detour cost (null when unverified).</summary>
public record DetourResult(TripEntity Trip, int? DetourSeconds);

public interface IDetourVerifier
{
    /// <summary>
    /// Verify and rank corridor candidates by detour cost.
    /// The top <paramref name="topN"/> candidates that have a stored route are checked against
    /// the Directions API. Trips exceeding <paramref name="maxDetourSeconds"/> are excluded.
    /// Trips without a stored route (or when the API fails) are appended unverified at the end
    /// with a null DetourSeconds.
    /// Results are sorted by DetourSeconds ascending; unverified trips trail.
    /// </summary>
    Task<IReadOnlyList<DetourResult>> VerifyAndRankAsync(
        IEnumerable<TripEntity> candidates,
        double pickupLat, double pickupLon,
        double dropoffLat, double dropoffLon,
        int maxDetourSeconds = 600,
        int topN = 10,
        CancellationToken ct = default);
}
