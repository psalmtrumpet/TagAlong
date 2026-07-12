using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using NetTopologySuite.Geometries;
using TagAlong.Trip.Domain.Entities;
using TagAlong.Trip.Infrastructure.Services;
using Xunit;
using TripEntity = TagAlong.Trip.Domain.Entities.Trip;

namespace TagAlong.Trip.Tests;

public class DetourVerifierTests
{
    private static readonly GeometryFactory Factory = new(new PrecisionModel(), 4326);

    private static TripEntity MakeTrip(TripRouteStatus routeStatus, int? originalDurationSeconds = null)
    {
        var trip = TripEntity.Create(
            Guid.NewGuid(), "Origin", 6.5, 3.3, "Dest", 6.6, 3.2,
            DateTime.UtcNow.AddHours(2), null, 0, "Car", null, null);

        if (routeStatus == TripRouteStatus.Stored && originalDurationSeconds.HasValue)
        {
            var line = Factory.CreateLineString(new[] { new Coordinate(3.3, 6.5), new Coordinate(3.2, 6.6) });
            trip.SetRoute(line, originalDurationSeconds.Value);
        }
        else
        {
            trip.MarkRouteStatus(routeStatus);
        }

        return trip;
    }

    private static DetourVerifier CreateVerifier(IGoogleDirectionsClient? client = null)
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        client ??= new NullDirectionsClient();
        return new DetourVerifier(client, cache, NullLogger<DetourVerifier>.Instance);
    }

    [Fact]
    public async Task VerifyAndRank_NoStoredRoutes_ReturnsAllPassThrough()
    {
        var trips = new[]
        {
            MakeTrip(TripRouteStatus.None),
            MakeTrip(TripRouteStatus.Failed),
            MakeTrip(TripRouteStatus.Pending),
        };

        var result = await CreateVerifier().VerifyAndRankAsync(trips, 6.5, 3.3, 6.6, 3.2);

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task VerifyAndRank_ApiFails_TripIncludedUnverified()
    {
        var trip = MakeTrip(TripRouteStatus.Stored, 600);
        var failingClient = new FailingDirectionsClient();

        var result = await CreateVerifier(failingClient).VerifyAndRankAsync(
            new[] { trip }, 6.5, 3.3, 6.6, 3.2, maxDetourSeconds: 600);

        // API failed → included unverified with null DetourSeconds
        Assert.Single(result);
        Assert.Equal(trip.Id, result[0].Trip.Id);
        Assert.Null(result[0].DetourSeconds);
    }

    [Fact]
    public async Task VerifyAndRank_DetourUnderLimit_Included()
    {
        var trip = MakeTrip(TripRouteStatus.Stored, 1200); // original = 20 min
        var client = new FixedDurationClient(1500); // detour route = 25 min → 5 min detour

        var result = await CreateVerifier(client).VerifyAndRankAsync(
            new[] { trip }, 6.5, 3.3, 6.6, 3.2, maxDetourSeconds: 600);

        Assert.Single(result);
    }

    [Fact]
    public async Task VerifyAndRank_DetourOverLimit_Excluded()
    {
        var trip = MakeTrip(TripRouteStatus.Stored, 600); // original = 10 min
        var client = new FixedDurationClient(2000); // detour = 33 min → 23 min detour → excluded

        var result = await CreateVerifier(client).VerifyAndRankAsync(
            new[] { trip }, 6.5, 3.3, 6.6, 3.2, maxDetourSeconds: 600);

        Assert.Empty(result);
    }

    [Fact]
    public async Task VerifyAndRank_SortsByDetourAscending()
    {
        var tripA = MakeTrip(TripRouteStatus.Stored, 1000); // original 1000s
        var tripB = MakeTrip(TripRouteStatus.Stored, 1000);

        // Return different durations for each trip via a dictionary-based stub
        var client = new SequentialDurationClient(new[] { 1200, 1050 }); // tripA +200s, tripB +50s

        var result = await CreateVerifier(client).VerifyAndRankAsync(
            new[] { tripA, tripB }, 6.5, 3.3, 6.6, 3.2, maxDetourSeconds: 600);

        Assert.Equal(2, result.Count);
        Assert.Equal(tripB.Id, result[0].Trip.Id); // tripB has smaller detour (50s < 200s)
        Assert.Equal(tripA.Id, result[1].Trip.Id);
        Assert.Equal(50, result[0].DetourSeconds);
        Assert.Equal(200, result[1].DetourSeconds);
    }

    [Fact]
    public async Task VerifyAndRank_CacheReusedForSameKey()
    {
        var trip = MakeTrip(TripRouteStatus.Stored, 1000);
        var countingClient = new CountingDirectionsClient(1100);

        var verifier = CreateVerifier(countingClient);
        await verifier.VerifyAndRankAsync(new[] { trip }, 6.5, 3.3, 6.6, 3.2);
        await verifier.VerifyAndRankAsync(new[] { trip }, 6.5, 3.3, 6.6, 3.2); // same coords

        Assert.Equal(1, countingClient.CallCount); // second call hits cache
    }

    // ── Test doubles ─────────────────────────────────────────────────────────

    private class NullDirectionsClient : IGoogleDirectionsClient
    {
        public Task<RouteInfo?> GetRouteAsync(double oLat, double oLon, double dLat, double dLon, CancellationToken ct = default) => Task.FromResult<RouteInfo?>(null);
        public Task<int?> GetDetourDurationAsync(double oLat, double oLon, double pLat, double pLon, double drLat, double drLon, double dLat, double dLon, CancellationToken ct = default) => Task.FromResult<int?>(null);
    }

    private class FailingDirectionsClient : IGoogleDirectionsClient
    {
        public Task<RouteInfo?> GetRouteAsync(double oLat, double oLon, double dLat, double dLon, CancellationToken ct = default) => throw new HttpRequestException("API down");
        public Task<int?> GetDetourDurationAsync(double oLat, double oLon, double pLat, double pLon, double drLat, double drLon, double dLat, double dLon, CancellationToken ct = default) => throw new HttpRequestException("API down");
    }

    private class FixedDurationClient : IGoogleDirectionsClient
    {
        private readonly int _seconds;
        public FixedDurationClient(int seconds) => _seconds = seconds;
        public Task<RouteInfo?> GetRouteAsync(double oLat, double oLon, double dLat, double dLon, CancellationToken ct = default) => Task.FromResult<RouteInfo?>(null);
        public Task<int?> GetDetourDurationAsync(double oLat, double oLon, double pLat, double pLon, double drLat, double drLon, double dLat, double dLon, CancellationToken ct = default) => Task.FromResult<int?>(_seconds);
    }

    private class SequentialDurationClient : IGoogleDirectionsClient
    {
        private readonly int[] _durations;
        private int _index;
        public SequentialDurationClient(int[] durations) => _durations = durations;
        public Task<RouteInfo?> GetRouteAsync(double oLat, double oLon, double dLat, double dLon, CancellationToken ct = default) => Task.FromResult<RouteInfo?>(null);
        public Task<int?> GetDetourDurationAsync(double oLat, double oLon, double pLat, double pLon, double drLat, double drLon, double dLat, double dLon, CancellationToken ct = default) => Task.FromResult<int?>(_durations[_index++]);
    }

    private class CountingDirectionsClient : IGoogleDirectionsClient
    {
        private readonly int _duration;
        public int CallCount { get; private set; }
        public CountingDirectionsClient(int duration) => _duration = duration;
        public Task<RouteInfo?> GetRouteAsync(double oLat, double oLon, double dLat, double dLon, CancellationToken ct = default) => Task.FromResult<RouteInfo?>(null);
        public Task<int?> GetDetourDurationAsync(double oLat, double oLon, double pLat, double pLon, double drLat, double drLon, double dLat, double dLon, CancellationToken ct = default)
        {
            CallCount++;
            return Task.FromResult<int?>(_duration);
        }
    }
}
