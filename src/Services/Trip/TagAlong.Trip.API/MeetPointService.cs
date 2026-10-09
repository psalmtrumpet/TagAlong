using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TagAlong.Trip.Infrastructure.Persistence;

namespace TagAlong.Trip.API;

/// <summary>A place on the driver's route where a passenger can be picked up or set down.</summary>
/// <param name="Kind">bus_stop, junction or road.</param>
/// <param name="WalkMeters">Rough walking distance for the passenger.</param>
/// <param name="AlongRouteMeters">How far along the driver's route it is (orders pickup before drop-off).</param>
public record MeetPointDto(double Latitude, double Longitude, string Name, string Kind, int WalkMeters, int AlongRouteMeters);

/// <summary>
/// Finds meet points for a passenger that keep the driver on their own route —
/// no detours. Candidates are bus stops right beside the route (OpenStreetMap)
/// and junctions the route passes through (OSRM turn-by-turn), plus the closest
/// point on the route itself as a fallback. Closest to the passenger first.
/// </summary>
public class MeetPointService
{
    private const double OnRouteMeters = 40;     // a bus stop counts as "on the route" within this
    private const double SearchRadiusMeters = 1500;
    private const int MaxResults = 6;

    private readonly IHttpClientFactory _http;
    private readonly IMemoryCache _cache;
    private readonly IDbContextFactory<TripDbContext> _dbFactory;
    private readonly ILogger<MeetPointService> _logger;

    public MeetPointService(IHttpClientFactory http, IMemoryCache cache, IDbContextFactory<TripDbContext> dbFactory, ILogger<MeetPointService> logger)
    {
        _http = http;
        _cache = cache;
        _dbFactory = dbFactory;
        _logger = logger;
    }

    /// <param name="afterLat">For drop-off: only points after this one (the pickup) along the route.</param>
    public async Task<IReadOnlyList<MeetPointDto>?> FindAsync(Guid tripId, double lat, double lng,
        double? afterLat, double? afterLng, CancellationToken ct)
    {
        var route = await GetRouteAsync(tripId, ct);
        if (route == null) return null;

        var (onRoute, along, toRoute) = route.Project(lat, lng);
        double? after = afterLat is { } al && afterLng is { } ag ? route.Project(al, ag).Along : null;

        var candidates = new List<MeetPointDto>();

        // Bus stops right beside the route, around the passenger's closest point on it
        foreach (var stop in await BusStopsAsync(onRoute.Lat, onRoute.Lng, ct))
        {
            var p = route.Project(stop.Lat, stop.Lng);
            if (p.Distance > OnRouteMeters) continue;
            candidates.Add(Make(stop.Lat, stop.Lng, stop.Name, "bus_stop", lat, lng, p.Along));
        }

        // Junctions the route itself passes through
        foreach (var j in route.Junctions)
        {
            if (Haversine(j.Lat, j.Lng, onRoute.Lat, onRoute.Lng) > SearchRadiusMeters) continue;
            candidates.Add(Make(j.Lat, j.Lng, j.Name, "junction", lat, lng, route.Project(j.Lat, j.Lng).Along));
        }

        // The closest point on the route — always available
        var roadName = route.RoadAt(along);
        candidates.Add(Make(onRoute.Lat, onRoute.Lng,
            string.IsNullOrWhiteSpace(roadName) ? "On the driver's route" : $"On {roadName}", "road", lat, lng, along));

        if (after is { } a)
            candidates = candidates.Where(c => c.AlongRouteMeters > a + 200).ToList();

        // Prefer a named stop or junction unless it means a much longer walk
        var ranked = candidates
            .OrderBy(c => c.WalkMeters + (c.Kind == "road" ? 300 : 0))
            .ToList();

        // Drop near-duplicates (same stop tagged twice, a stop at a junction…)
        var result = new List<MeetPointDto>();
        foreach (var c in ranked)
        {
            if (result.Any(r => Haversine(r.Latitude, r.Longitude, c.Latitude, c.Longitude) < 60)) continue;
            result.Add(c);
            if (result.Count == MaxResults) break;
        }
        _logger.LogInformation("Meet points for trip {TripId}: {Count} ({ToRoute:0} m from route)", tripId, result.Count, toRoute);
        return result;
    }

    private static MeetPointDto Make(double lat, double lng, string name, string kind, double fromLat, double fromLng, double along) =>
        new(Math.Round(lat, 6), Math.Round(lng, 6), name, kind,
            (int)Math.Round(Haversine(fromLat, fromLng, lat, lng) * 1.3), (int)Math.Round(along));

    // ── The driver's route ─────────────────────────────────────────────────

    private async Task<RouteData?> GetRouteAsync(Guid tripId, CancellationToken ct)
    {
        if (_cache.TryGetValue($"meet-route:{tripId}", out RouteData? cached)) return cached;

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var trip = await db.Trips.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tripId, ct);
        if (trip == null) return null;

        var route = await FetchRouteAsync(trip.OriginLatitude, trip.OriginLongitude, trip.DestinationLatitude, trip.DestinationLongitude, ct);
        if (route != null) _cache.Set($"meet-route:{tripId}", route, TimeSpan.FromHours(6));
        return route;
    }

    private async Task<RouteData?> FetchRouteAsync(double oLat, double oLng, double dLat, double dLng, CancellationToken ct)
    {
        try
        {
            var c = CultureInfo.InvariantCulture;
            var url = $"https://router.project-osrm.org/route/v1/driving/{oLng.ToString(c)},{oLat.ToString(c)};{dLng.ToString(c)},{dLat.ToString(c)}" +
                      "?overview=full&geometries=geojson&steps=true";
            using var res = await _http.CreateClient("osrm").GetAsync(url, ct);
            if (!res.IsSuccessStatusCode) return null;
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
            if (doc.RootElement.GetProperty("code").GetString() != "Ok") return null;
            var route = doc.RootElement.GetProperty("routes")[0];

            var points = route.GetProperty("geometry").GetProperty("coordinates").EnumerateArray()
                .Select(p => (Lat: p[1].GetDouble(), Lng: p[0].GetDouble())).ToList();

            var junctions = new List<(double Lat, double Lng, string Name)>();
            var roads = new List<(double From, string Name)>();
            double travelled = 0;
            foreach (var leg in route.GetProperty("legs").EnumerateArray())
            {
                string? prev = null;
                foreach (var step in leg.GetProperty("steps").EnumerateArray())
                {
                    var name = step.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                    var type = step.GetProperty("maneuver").GetProperty("type").GetString();
                    var loc = step.GetProperty("maneuver").GetProperty("location");
                    if (type is not ("depart" or "arrive") && !string.IsNullOrWhiteSpace(prev) &&
                        !string.IsNullOrWhiteSpace(name) && !string.Equals(prev, name, StringComparison.OrdinalIgnoreCase))
                        junctions.Add((loc[1].GetDouble(), loc[0].GetDouble(), $"{prev} / {name} junction"));
                    roads.Add((travelled, name));
                    travelled += step.GetProperty("distance").GetDouble();
                    if (!string.IsNullOrWhiteSpace(name)) prev = name;
                }
            }
            return new RouteData(points, junctions, roads);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Meet points: couldn't fetch the route");
            return null;
        }
    }

    // ── Bus stops (OpenStreetMap) ──────────────────────────────────────────

    private async Task<List<(double Lat, double Lng, string Name)>> BusStopsAsync(double lat, double lng, CancellationToken ct)
    {
        var key = $"stops:{Math.Round(lat, 3)}:{Math.Round(lng, 3)}";
        if (_cache.TryGetValue(key, out List<(double, double, string)>? cached)) return cached!;

        var stops = new List<(double Lat, double Lng, string Name)>();
        try
        {
            var c = CultureInfo.InvariantCulture;
            var around = $"around:{SearchRadiusMeters.ToString(c)},{lat.ToString(c)},{lng.ToString(c)}";
            var query = $"[out:json][timeout:15];(node[\"highway\"=\"bus_stop\"]({around});node[\"public_transport\"=\"platform\"]({around}););out body;";
            using var res = await _http.CreateClient("overpass").PostAsync("https://overpass-api.de/api/interpreter",
                new FormUrlEncodedContent(new Dictionary<string, string> { ["data"] = query }), ct);
            if (res.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
                foreach (var e in doc.RootElement.GetProperty("elements").EnumerateArray())
                {
                    var name = e.TryGetProperty("tags", out var tags) && tags.TryGetProperty("name", out var n) ? n.GetString() : null;
                    stops.Add((e.GetProperty("lat").GetDouble(), e.GetProperty("lon").GetDouble(),
                        string.IsNullOrWhiteSpace(name) ? "Bus stop" : $"{name.Trim()} bus stop"));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Meet points: bus stop lookup failed");
        }
        _cache.Set(key, stops, TimeSpan.FromHours(12));
        return stops;
    }

    // ── Geometry ───────────────────────────────────────────────────────────

    internal static double Haversine(double lat1, double lng1, double lat2, double lng2)
    {
        const double R = 6371000;
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLng = (lng2 - lng1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return 2 * R * Math.Asin(Math.Sqrt(a));
    }

    private sealed class RouteData
    {
        private readonly List<(double Lat, double Lng)> _points;
        private readonly double[] _cum;
        private readonly List<(double From, string Name)> _roads;
        public List<(double Lat, double Lng, string Name)> Junctions { get; }

        public RouteData(List<(double Lat, double Lng)> points, List<(double, double, string)> junctions, List<(double From, string Name)> roads)
        {
            _points = points;
            Junctions = junctions;
            _roads = roads;
            _cum = new double[points.Count];
            for (var i = 1; i < points.Count; i++)
                _cum[i] = _cum[i - 1] + Haversine(points[i - 1].Lat, points[i - 1].Lng, points[i].Lat, points[i].Lng);
        }

        /// <summary>Closest point on the route, how far along the route it is, and how far away.</summary>
        public ((double Lat, double Lng) Point, double Along, double Distance) Project(double lat, double lng)
        {
            var best = ((Lat: _points[0].Lat, Lng: _points[0].Lng), 0.0, double.MaxValue);
            var mPerLat = 111_320.0;
            var mPerLng = 111_320.0 * Math.Cos(lat * Math.PI / 180);
            for (var i = 1; i < _points.Count; i++)
            {
                var (aLat, aLng) = _points[i - 1];
                var (bLat, bLng) = _points[i];
                // Work in local metres around the passenger
                double ax = (aLng - lng) * mPerLng, ay = (aLat - lat) * mPerLat;
                double bx = (bLng - lng) * mPerLng, by = (bLat - lat) * mPerLat;
                double dx = bx - ax, dy = by - ay, len2 = dx * dx + dy * dy;
                var t = len2 == 0 ? 0 : Math.Clamp(-(ax * dx + ay * dy) / len2, 0, 1);
                double px = ax + t * dx, py = ay + t * dy;
                var dist = Math.Sqrt(px * px + py * py);
                if (dist < best.Item3)
                {
                    var segLen = _cum[i] - _cum[i - 1];
                    best = ((lat + py / mPerLat, lng + px / mPerLng), _cum[i - 1] + t * segLen, dist);
                }
            }
            return best;
        }

        public string? RoadAt(double along) =>
            _roads.LastOrDefault(r => r.From <= along && !string.IsNullOrWhiteSpace(r.Name)).Name;
    }
}
