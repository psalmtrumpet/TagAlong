using StackExchange.Redis;

namespace TagAlong.User.API.Services;

/// <summary>
/// Every available driver's latest position in Redis (a geo set), so "who's
/// near this passenger" is one fast lookup instead of a database scan. A second
/// sorted set holds when each driver was last heard from: drivers that go quiet
/// for <see cref="Fresh"/> drop out (their app checks in every few minutes while
/// available). The database stays the source of truth — when Redis is down every
/// call here quietly does nothing and lookups fall back to the database.
/// </summary>
public class DriverGeoIndex
{
    public const string GeoKey = "geo:drivers:available";
    public const string SeenKey = "geo:drivers:seen";

    /// <summary>A driver not heard from for this long isn't offered to passengers.</summary>
    public static readonly TimeSpan Fresh = TimeSpan.FromMinutes(30);

    private readonly IConnectionMultiplexer? _redis;
    private readonly ILogger<DriverGeoIndex> _logger;

    public DriverGeoIndex(IServiceProvider services, ILogger<DriverGeoIndex> logger)
    {
        _redis = services.GetService<IConnectionMultiplexer>();
        _logger = logger;
    }

    private IDatabase? Db => _redis is { IsConnected: true } r ? r.GetDatabase() : null;

    public async Task SetAsync(Guid driverId, double lat, double lng, DateTime seenUtc)
    {
        if (Db is not { } db) return;
        try
        {
            var member = driverId.ToString();
            var batch = db.CreateBatch();
            var geo = batch.GeoAddAsync(GeoKey, lng, lat, member);
            var seen = batch.SortedSetAddAsync(SeenKey, member, ToUnix(seenUtc));
            batch.Execute();
            await Task.WhenAll(geo, seen);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Driver geo index: couldn't store {DriverId}", driverId);
        }
    }

    public async Task RemoveAsync(Guid driverId)
    {
        if (Db is not { } db) return;
        try
        {
            var member = driverId.ToString();
            await Task.WhenAll(db.SortedSetRemoveAsync(GeoKey, member), db.SortedSetRemoveAsync(SeenKey, member));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Driver geo index: couldn't remove {DriverId}", driverId);
        }
    }

    /// <summary>
    /// Available drivers heard from recently within <paramref name="radiusKm"/>,
    /// nearest first. Null when Redis can't answer (the caller uses the database).
    /// </summary>
    public async Task<List<(Guid DriverId, double Km)>?> NearAsync(double lat, double lng, double radiusKm, int max = 200)
    {
        if (Db is not { } db) return null;
        try
        {
            var hits = await db.GeoSearchAsync(GeoKey, lng, lat, new GeoSearchCircle(radiusKm, GeoUnit.Kilometers),
                count: max, order: Order.Ascending, options: GeoRadiusOptions.WithDistance);
            if (hits.Length == 0) return new();
            var seen = await db.SortedSetScoresAsync(SeenKey, hits.Select(h => h.Member).ToArray());
            var freshSince = ToUnix(DateTime.UtcNow - Fresh);
            var list = new List<(Guid, double)>();
            for (var i = 0; i < hits.Length; i++)
            {
                if (seen[i] is not { } s || s < freshSince) continue;
                if (Guid.TryParse(hits[i].Member.ToString(), out var id)) list.Add((id, hits[i].Distance ?? 0));
            }
            return list;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Driver geo index: lookup failed, using the database");
            return null;
        }
    }

    /// <summary>
    /// Make Redis match the database: <paramref name="live"/> is every available
    /// driver with a recent position. Anyone else (went offline, went quiet) is removed.
    /// </summary>
    public async Task ReconcileAsync(IReadOnlyCollection<(Guid DriverId, double Lat, double Lng, DateTime SeenUtc)> live)
    {
        if (Db is not { } db) return;
        try
        {
            var keep = live.Select(l => l.DriverId.ToString()).ToHashSet();
            var indexed = await db.SortedSetRangeByRankAsync(SeenKey);
            var gone = indexed.Where(m => !keep.Contains(m.ToString())).ToArray();
            if (gone.Length > 0)
                await Task.WhenAll(db.SortedSetRemoveAsync(GeoKey, gone), db.SortedSetRemoveAsync(SeenKey, gone));
            foreach (var l in live) await SetAsync(l.DriverId, l.Lat, l.Lng, l.SeenUtc);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Driver geo index: reconcile failed");
        }
    }

    private static double ToUnix(DateTime utc) =>
        new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeSeconds();
}
