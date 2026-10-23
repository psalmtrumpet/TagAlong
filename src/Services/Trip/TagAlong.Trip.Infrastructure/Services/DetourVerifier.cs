using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using TagAlong.Trip.Domain.Entities;
using TripEntity = TagAlong.Trip.Domain.Entities.Trip;

namespace TagAlong.Trip.Infrastructure.Services;

public class DetourVerifier : IDetourVerifier
{
    private readonly IGoogleDirectionsClient _directions;
    private readonly IMemoryCache _cache;
    private readonly ILogger<DetourVerifier> _logger;

    private static readonly MemoryCacheEntryOptions _cacheOpts =
        new MemoryCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromHours(1));

    public DetourVerifier(
        IGoogleDirectionsClient directions,
        IMemoryCache cache,
        ILogger<DetourVerifier> logger)
    {
        _directions = directions;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IReadOnlyList<DetourResult>> VerifyAndRankAsync(
        IEnumerable<TripEntity> candidates,
        double pickupLat, double pickupLon,
        double dropoffLat, double dropoffLon,
        int maxDetourSeconds = 600,
        int topN = 10,
        CancellationToken ct = default)
    {
        var all = candidates.ToList();

        // Split into verifiable (have stored route + original duration) and the rest.
        var verifiable = all
            .Where(t => t.RouteStatus == TripRouteStatus.Stored && t.OriginalDurationSeconds.HasValue)
            .Take(topN)
            .ToList();

        var unverifiableSet = new HashSet<Guid>(all.Select(t => t.Id));
        verifiable.ForEach(t => unverifiableSet.Remove(t.Id));
        var unverified = all.Where(t => unverifiableSet.Contains(t.Id)).ToList();

        var ranked = new List<(TripEntity Trip, int DetourSeconds)>();

        foreach (var trip in verifiable)
        {
            try
            {
                var detour = await GetDetourSecondsAsync(
                    trip, pickupLat, pickupLon, dropoffLat, dropoffLon, ct);

                if (detour <= maxDetourSeconds)
                    ranked.Add((trip, detour));
                else
                    _logger.LogDebug("Trip {TripId} excluded: {Detour}s > {Max}s max detour",
                        trip.Id, detour, maxDetourSeconds);
            }
            catch (Exception ex)
            {
                // Directions timed out or failed: estimate from straight-line distances rather
                // than letting the trip through unchecked — otherwise it appears and vanishes
                // on each refresh as Google answers or doesn't.
                var estimate = EstimateDetourSeconds(trip, pickupLat, pickupLon, dropoffLat, dropoffLon);
                _logger.LogWarning(ex, "Detour check failed for trip {TripId}; estimated {Detour}s", trip.Id, estimate);
                if (estimate <= maxDetourSeconds)
                    ranked.Add((trip, estimate));
            }
        }

        return ranked
            .OrderBy(x => x.DetourSeconds)
            .Select(x => new DetourResult(x.Trip, x.DetourSeconds))
            .Concat(unverified.Select(t => new DetourResult(t, null)))
            .ToList();
    }

    /// <summary>
    /// Rough detour when Directions can't answer: extra straight-line distance via the
    /// pickup and drop-off, ×1.3 for roads, at 25 km/h Lagos traffic.
    /// </summary>
    private static int EstimateDetourSeconds(TripEntity trip, double pLat, double pLon, double dLat, double dLon)
    {
        static double Km(double lat1, double lon1, double lat2, double lon2)
        {
            const double r = 6371;
            var dLatR = (lat2 - lat1) * Math.PI / 180;
            var dLonR = (lon2 - lon1) * Math.PI / 180;
            var a = Math.Sin(dLatR / 2) * Math.Sin(dLatR / 2) +
                    Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) * Math.Sin(dLonR / 2) * Math.Sin(dLonR / 2);
            return 2 * r * Math.Asin(Math.Sqrt(a));
        }
        var via = Km(trip.OriginLatitude, trip.OriginLongitude, pLat, pLon) + Km(pLat, pLon, dLat, dLon) +
                  Km(dLat, dLon, trip.DestinationLatitude, trip.DestinationLongitude);
        var direct = Km(trip.OriginLatitude, trip.OriginLongitude, trip.DestinationLatitude, trip.DestinationLongitude);
        var extraKm = Math.Max(0, via - direct) * 1.3;
        return (int)Math.Round(extraKm / 25.0 * 3600);
    }

    private async Task<int> GetDetourSecondsAsync(
        TripEntity trip,
        double pickupLat, double pickupLon,
        double dropoffLat, double dropoffLon,
        CancellationToken ct)
    {
        // Cache key: (tripId, pickup rounded to 3 dp, dropoff rounded to 3 dp)
        // 3 decimal places ≈ 111 m resolution — fine for city-level caching.
        var cacheKey = $"detour:{trip.Id}:" +
                       $"{pickupLat:F3}_{pickupLon:F3}:" +
                       $"{dropoffLat:F3}_{dropoffLon:F3}";

        if (_cache.TryGetValue(cacheKey, out int cached))
            return cached;

        var detourDuration = await _directions.GetDetourDurationAsync(
            trip.OriginLatitude, trip.OriginLongitude,
            pickupLat, pickupLon,
            dropoffLat, dropoffLon,
            trip.DestinationLatitude, trip.DestinationLongitude,
            ct);

        if (detourDuration is null)
            throw new InvalidOperationException("Directions API returned no result");

        var detourSeconds = Math.Max(0, detourDuration.Value - trip.OriginalDurationSeconds!.Value);
        _cache.Set(cacheKey, detourSeconds, _cacheOpts);

        return detourSeconds;
    }
}
