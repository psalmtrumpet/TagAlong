using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TagAlong.Trip.Infrastructure.Persistence;

namespace TagAlong.Trip.API;

/// <param name="Summary">Main roads, e.g. "Mobolaji Bank Anthony Way".</param>
/// <param name="DurationInTrafficSeconds">Expected time with current traffic (same as Duration when unknown).</param>
/// <param name="Polyline">Google-encoded route line.</param>
/// <param name="Fastest">Quickest in current traffic.</param>
/// <param name="LeastTraffic">Smallest delay compared with a clear road.</param>
public record RouteOptionDto(
    string Summary,
    int DistanceMeters,
    int DurationSeconds,
    int DurationInTrafficSeconds,
    string Polyline,
    bool Fastest,
    bool LeastTraffic);

/// <summary>
/// Road routes from Google Directions with live traffic: the choices for a new
/// trip, and the live route a map follows during a ride. Every call is counted
/// per trip (<see cref="GoogleCallLog"/>). There is no free fallback: when Google
/// fails the caller gets nothing and the app shows a marked straight line.
/// </summary>
public class RouteOptionsService
{
    private readonly IHttpClientFactory _http;
    private readonly IConfiguration _config;
    private readonly ILogger<RouteOptionsService> _logger;
    private readonly GoogleCallLog _calls;
    private readonly TripDbContext _db;

    public RouteOptionsService(IHttpClientFactory http, IConfiguration config, ILogger<RouteOptionsService> logger, GoogleCallLog calls,
        TripDbContext db)
    {
        _db = db;
        _http = http;
        _config = config;
        _logger = logger;
        _calls = calls;
    }

    /// <summary>Most places a driver can route through.</summary>
    public const int MaxVias = 3;

    public async Task<List<RouteOptionDto>> GetAsync(double oLat, double oLng, double dLat, double dLng,
        IReadOnlyList<(double Lat, double Lng)> vias, DateTime? departure, CancellationToken ct, Guid? userId = null)
    {
        var raw = await GoogleAsync(oLat, oLng, dLat, dLng, vias, departure, alternatives: true, ct);
        await _calls.RecordAsync("route-options", raw != null, userId);
        if (raw == null || raw.Count == 0) return new List<RouteOptionDto>();

        var fastest = raw.MinBy(r => r.DurationInTrafficSeconds)!;
        var hasTraffic = raw.Any(r => r.DurationInTrafficSeconds != r.DurationSeconds);
        var leastTraffic = hasTraffic && raw.Count > 1
            ? raw.MinBy(r => r.DurationInTrafficSeconds - r.DurationSeconds)
            : null;

        return raw
            .Select(r => r with { Fastest = ReferenceEquals(r, fastest), LeastTraffic = ReferenceEquals(r, leastTraffic) })
            .OrderBy(r => r.DurationInTrafficSeconds)
            .ToList();
    }

    /// <summary>The one route a live map follows right now (traffic as of now), or null.</summary>
    public async Task<RouteOptionDto?> LiveAsync(double oLat, double oLng, double dLat, double dLng,
        Guid? userId, Guid? tripId, Guid? conversationId, CancellationToken ct)
    {
        // On a trip, keep to the road the driver chose (Google's quickest may differ)
        IReadOnlyList<(double Lat, double Lng)> vias = Array.Empty<(double, double)>();
        if (tripId is { } tid)
        {
            try
            {
                var line = await _db.Trips.AsNoTracking().Where(t => t.Id == tid).Select(t => t.RouteLine).FirstOrDefaultAsync(ct);
                if (line != null) vias = AlongChosenRoad(line.Coordinates.Select(c => (c.Y, c.X)).ToList(), oLat, oLng, dLat, dLng);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Live route: couldn't read trip {TripId}'s road", tid);
            }
        }
        var raw = await GoogleAsync(oLat, oLng, dLat, dLng, vias, null, alternatives: false, ct);
        await _calls.RecordAsync("live-route", raw != null, userId, tripId, conversationId);
        return raw?.FirstOrDefault();
    }

    /// <summary>
    /// Points to pass through so Google follows the chosen road from here to
    /// there: one roughly every 2 km (at most 8), between where the car and the
    /// target meet that road. None when either is off it (the car took another
    /// road, or a door-to-door pickup), or the stretch is short.
    /// </summary>
    public static List<(double Lat, double Lng)> AlongChosenRoad(IReadOnlyList<(double Lat, double Lng)> road,
        double oLat, double oLng, double dLat, double dLng)
    {
        var none = new List<(double, double)>();
        if (road.Count < 2) return none;
        int Nearest(double lat, double lng, double maxKm)
        {
            var best = -1;
            var bestKm = double.MaxValue;
            for (var i = 0; i < road.Count; i++)
            {
                var km = HaversineKm(lat, lng, road[i].Lat, road[i].Lng);
                if (km < bestKm) { bestKm = km; best = i; }
            }
            return bestKm <= maxKm ? best : -1;
        }
        var from = Nearest(oLat, oLng, 0.4);
        var to = Nearest(dLat, dLng, 0.4);
        if (from < 0 || to < 0 || to <= from + 1) return none;

        var along = new List<double> { 0 };
        for (var i = from + 1; i <= to; i++)
            along.Add(along[^1] + HaversineKm(road[i - 1].Lat, road[i - 1].Lng, road[i].Lat, road[i].Lng));
        var total = along[^1];
        if (total < 1.5) return none;

        var count = Math.Clamp((int)(total / 2.0), 1, 8);
        var vias = new List<(double, double)>();
        for (var n = 1; n <= count; n++)
        {
            var want = total * n / (count + 1);
            var k = along.FindIndex(a => a >= want);
            if (k < 0) k = along.Count - 1;
            vias.Add(road[from + k]);
        }
        return vias;
    }

    private static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double r = 6371;
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLon = (lon2 - lon1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return r * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private async Task<List<RouteOptionDto>?> GoogleAsync(double oLat, double oLng, double dLat, double dLng,
        IReadOnlyList<(double Lat, double Lng)> vias, DateTime? departure, bool alternatives, CancellationToken ct)
    {
        var key = _config["GoogleMaps:ApiKey"];
        if (string.IsNullOrWhiteSpace(key)) return null;
        try
        {
            var c = CultureInfo.InvariantCulture;
            // Traffic needs a departure time that isn't in the past
            var when = departure is { } d && d.ToUniversalTime() > DateTime.UtcNow
                ? new DateTimeOffset(d.ToUniversalTime()).ToUnixTimeSeconds().ToString(c)
                : "now";
            var url = "https://maps.googleapis.com/maps/api/directions/json" +
                      $"?origin={oLat.ToString(c)},{oLng.ToString(c)}&destination={dLat.ToString(c)},{dLng.ToString(c)}" +
                      $"&departure_time={when}&traffic_model=best_guess&key={key}";
            // Google only offers alternatives when there's no stop in between
            if (vias.Count > 0)
                url += "&waypoints=" + string.Join("|", vias.Select(v => $"via:{v.Lat.ToString(c)},{v.Lng.ToString(c)}"));
            else if (alternatives)
                url += "&alternatives=true";

            using var res = await _http.CreateClient("osrm").GetAsync(url, ct);
            if (!res.IsSuccessStatusCode) return null;
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
            var status = doc.RootElement.GetProperty("status").GetString();
            if (status != "OK")
            {
                _logger.LogWarning("Directions: Google said {Status}", status);
                return null;
            }

            var list = new List<RouteOptionDto>();
            foreach (var r in doc.RootElement.GetProperty("routes").EnumerateArray())
            {
                int dist = 0, dur = 0, traffic = 0;
                foreach (var leg in r.GetProperty("legs").EnumerateArray())
                {
                    dist += leg.GetProperty("distance").GetProperty("value").GetInt32();
                    var legDur = leg.GetProperty("duration").GetProperty("value").GetInt32();
                    dur += legDur;
                    traffic += leg.TryGetProperty("duration_in_traffic", out var t) ? t.GetProperty("value").GetInt32() : legDur;
                }
                var summary = r.TryGetProperty("summary", out var s) && !string.IsNullOrWhiteSpace(s.GetString()) ? s.GetString()! : "Suggested route";
                list.Add(new RouteOptionDto(summary, dist, dur, traffic,
                    r.GetProperty("overview_polyline").GetProperty("points").GetString() ?? "", false, false));
            }
            return list.Count > 0 ? list : null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Directions: Google lookup failed");
            return null;
        }
    }
}
